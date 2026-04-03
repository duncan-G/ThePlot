import {
  Component,
  signal,
  computed,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
  PLATFORM_ID,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  ScreenplayService,
  ImportEvent,
  ScreenplayData,
  ScreenplayScene,
  ScreenplayElement,
  CharacterInfo,
} from '../../lib/services/screenplay.service';
import {
  ContentGenerationService,
  NodeStatusInfo,
  RunStatusUpdateEvent,
  ElementGenStatus,
  RunSummaryInfo,
  NodeDetailInfo,
  RunDetailsInfo,
} from '../../lib/services/content-generation.service';
import {
  AudioPlaybackService,
  PlaybackQueueItem,
} from '../../lib/services/audio-playback.service';
import { ThemeService } from '../../lib/services/theme.service';

type ViewState = 'importing' | 'viewing' | 'error';
type GenerationState = 'idle' | 'starting' | 'running' | 'completed' | 'failed';
type ActiveTab = 'screenplay' | 'characters' | 'generation';
type NodeFilter = 'all' | 'succeeded' | 'failed' | 'pending';

interface SceneNodeGroup {
  sceneId: string;
  sceneLabel: string;
  sceneIndex: number;
  nodes: NodeDetailInfo[];
}

interface ChunkPageRange {
  startPage: number;
  endPage: number;
  status: 'pending' | 'splitting' | 'processing' | 'done' | 'failed';
}

interface GenBlockEntry {
  element: ScreenplayElement;
  showCharCue: boolean;
  isVoiceOver: boolean;
}

interface GenBlock {
  nodeId: string | undefined;
  status: ElementGenStatus;
  kind: string | undefined;
  elements: GenBlockEntry[];
}

@Component({
  selector: 'app-screenplay-viewer',
  imports: [RouterLink],
  templateUrl: './screenplay-viewer.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScreenplayViewer implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly screenplayService = inject(ScreenplayService);
  private readonly contentGenService = inject(ContentGenerationService);
  protected readonly audioPlayback = inject(AudioPlaybackService);
  protected readonly themeService = inject(ThemeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly view = signal<ViewState>('viewing');
  protected readonly error = signal<string | null>(null);
  protected readonly importPhase = signal('Loading…');
  protected readonly screenplayId = signal<string | null>(null);
  protected readonly totalPages = signal(0);
  protected readonly chunks = signal<ChunkPageRange[]>([]);
  protected readonly screenplay = signal<ScreenplayData | null>(null);
  protected readonly activeSceneIndex = signal(0);
  protected readonly settingsOpen = signal(false);
  protected readonly activeTab = signal<ActiveTab>('screenplay');

  // Generation state
  protected readonly generationRunId = signal<string | null>(null);
  protected readonly generationStatus = signal<GenerationState>('idle');
  protected readonly generationError = signal<string | null>(null);
  protected readonly nodeStatuses = signal<Map<string, NodeStatusInfo>>(new Map());
  protected readonly progressPopupOpen = signal(false);
  // Generation history tab
  protected readonly generationRuns = signal<RunSummaryInfo[]>([]);
  protected readonly generationRunsLoading = signal(false);
  protected readonly selectedRunDetails = signal<RunDetailsInfo | null>(null);
  protected readonly selectedRunDetailsLoading = signal(false);
  protected readonly nodeStatusFilter = signal<NodeFilter>('all');
  protected readonly collapsedScenes = signal<Set<string>>(new Set());

  protected readonly nodeFilterCounts = computed(() => {
    const details = this.selectedRunDetails();
    if (!details) return { all: 0, succeeded: 0, failed: 0, pending: 0 };
    const nodes = details.nodes.filter(n => n.kind !== 'PreGenerationAnalysis');
    return {
      all: nodes.length,
      succeeded: nodes.filter(n => n.status === 'Succeeded').length,
      failed: nodes.filter(n => n.status === 'Failed' || n.status === 'Blocked').length,
      pending: nodes.filter(n => ['Pending', 'Ready', 'NeedsRetry', 'Running'].includes(n.status)).length,
    };
  });

  protected readonly groupedNodes = computed((): SceneNodeGroup[] => {
    const details = this.selectedRunDetails();
    if (!details) return [];
    const filter = this.nodeStatusFilter();
    const sceneList = this.scenes();
    const sceneMap = new Map(sceneList.map((s, i) => [
      s.id,
      { label: s.location || s.heading || `Scene ${i + 1}`, index: i, locationType: s.locationType },
    ]));

    const elementOrder = new Map<string, number>();
    let docOrd = 0;
    for (const s of sceneList) {
      for (const el of s.elements) {
        elementOrder.set(el.id, docOrd++);
      }
    }
    const nodeDocOrder = (node: NodeDetailInfo): number => {
      let min = Number.MAX_SAFE_INTEGER;
      for (const id of node.elementIds) {
        const o = elementOrder.get(id);
        if (o !== undefined && o < min) min = o;
      }
      return min === Number.MAX_SAFE_INTEGER ? Number.MAX_SAFE_INTEGER : min;
    };

    const nodes = details.nodes.filter(n => n.kind !== 'PreGenerationAnalysis');
    const filtered = filter === 'all' ? nodes : nodes.filter(n => {
      if (filter === 'succeeded') return n.status === 'Succeeded';
      if (filter === 'failed') return n.status === 'Failed' || n.status === 'Blocked';
      if (filter === 'pending') return ['Pending', 'Ready', 'NeedsRetry', 'Running'].includes(n.status);
      return true;
    });

    const groups = new Map<string, SceneNodeGroup>();
    for (const node of filtered) {
      const sid = node.sceneId || '__no_scene__';
      if (!groups.has(sid)) {
        const scene = sceneMap.get(sid);
        groups.set(sid, {
          sceneId: sid,
          sceneLabel: scene?.label ?? 'Unknown Scene',
          sceneIndex: scene?.index ?? 9999,
          nodes: [],
        });
      }
      groups.get(sid)!.nodes.push(node);
    }
    for (const g of groups.values()) {
      g.nodes.sort((a, b) => nodeDocOrder(a) - nodeDocOrder(b));
    }
    return [...groups.values()].sort((a, b) => a.sceneIndex - b.sceneIndex);
  });

  protected readonly processedPages = computed(() => {
    const done = this.chunks().filter(c => c.status === 'done');
    return done.reduce((sum, c) => sum + (c.endPage - c.startPage + 1), 0);
  });

  protected readonly scenes = computed(() => this.screenplay()?.scenes ?? []);
  protected readonly characters = computed(() => this.screenplay()?.characters ?? []);

  protected readonly isAllDone = computed(() => {
    const c = this.chunks();
    return c.length > 0 && c.every(ch => ch.status === 'done' || ch.status === 'failed');
  });

  protected readonly elementStatuses = computed(() => {
    const map = new Map<string, ElementGenStatus>();
    for (const node of this.nodeStatuses().values()) {
      const elStatus = this.nodeStatusToElementStatus(node.status);
      for (const eid of node.elementIds) {
        map.set(eid, elStatus);
      }
    }
    return map;
  });

  protected readonly elementToNodeId = computed(() => {
    const map = new Map<string, string>();
    for (const node of this.nodeStatuses().values()) {
      for (const eid of node.elementIds) {
        map.set(eid, node.nodeId);
      }
    }
    return map;
  });

  /** Nodes for progress UI: scene order, then document order within scene. */
  protected readonly orderedProgressNodes = computed((): NodeStatusInfo[] => {
    const sceneList = this.scenes();
    const sceneOrder = new Map(sceneList.map((s, i) => [s.id, i]));
    const elementOrder = new Map<string, number>();
    let docOrd = 0;
    for (const s of sceneList) {
      for (const el of s.elements) {
        elementOrder.set(el.id, docOrd++);
      }
    }
    const list = [...this.nodeStatuses().values()].filter(n => n.kind !== 'PreGenerationAnalysis');
    const key = (n: NodeStatusInfo) => {
      const si = n.sceneId ? (sceneOrder.get(n.sceneId) ?? 9999) : 9999;
      let eo = Number.MAX_SAFE_INTEGER;
      for (const id of n.elementIds) {
        const o = elementOrder.get(id);
        if (o !== undefined && o < eo) eo = o;
      }
      if (eo === Number.MAX_SAFE_INTEGER) eo = 999999999;
      return { si, eo };
    };
    list.sort((a, b) => {
      const ka = key(a);
      const kb = key(b);
      if (ka.si !== kb.si) return ka.si - kb.si;
      return ka.eo - kb.eo;
    });
    return list;
  });

  protected readonly generationProgress = computed(() => {
    const nodes = this.nodeStatuses();
    if (nodes.size === 0) return { total: 0, succeeded: 0, failed: 0, running: 0 };
    let total = 0, succeeded = 0, failed = 0, running = 0;
    for (const n of nodes.values()) {
      if (n.kind === 'PreGenerationAnalysis') continue;
      total++;
      if (n.status === 'Succeeded') succeeded++;
      else if (n.status === 'Failed') failed++;
      else if (n.status === 'Running') running++;
    }
    return { total, succeeded, failed, running };
  });

  protected readonly isGenerating = computed(() => {
    const s = this.generationStatus();
    return s === 'starting' || s === 'running';
  });

  protected readonly hasGeneration = computed(() => this.nodeStatuses().size > 0);

  protected readonly hasGeneratedContent = computed(() => {
    const nodes = this.nodeStatuses();
    if (nodes.size === 0) return false;
    for (const n of nodes.values()) {
      if (n.kind === 'PreGenerationAnalysis') continue;
      if (n.status === 'Succeeded') return true;
    }
    return false;
  });

  protected readonly canContinueGeneration = computed(() => {
    const runId = this.generationRunId();
    if (!runId || this.isGenerating()) return false;
    const progress = this.generationProgress();
    return progress.total > 0 && progress.succeeded < progress.total;
  });

  protected readonly playbackQueue = computed<PlaybackQueueItem[]>(() => {
    const allScenes = this.scenes();
    const elToNode = this.elementToNodeId();
    const nodeMap = this.nodeStatuses();
    if (allScenes.length === 0 || elToNode.size === 0) return [];

    const items: PlaybackQueueItem[] = [];
    const visitedNodes = new Set<string>();

    for (const scene of allScenes) {
      for (const el of scene.elements) {
        const nodeId = elToNode.get(el.id);
        if (!nodeId || visitedNodes.has(nodeId)) continue;
        const node = nodeMap.get(nodeId);
        if (!node || node.status !== 'Succeeded') continue;

        visitedNodes.add(nodeId);
        items.push({
          nodeId,
          elementIds: node.elementIds,
          sceneId: node.sceneId,
          kind: node.kind,
        });
      }
    }
    return items;
  });

  private statusSub: Subscription | null = null;
  private generationSub: Subscription | null = null;

  ngOnInit(): void {
    this.themeService.init();
    if (!this.isBrowser) return;
    const id = this.route.snapshot.paramMap.get('id');
    const blob = this.route.snapshot.queryParamMap.get('blob');
    if (id) {
      this.screenplayId.set(id);
      this.fetchScreenplayData(id);
      this.checkExistingGeneration(id);
      if (blob) {
        this.subscribeToStatus(blob);
      }
    } else {
      this.router.navigate(['/']);
    }
  }

  ngOnDestroy(): void {
    this.statusSub?.unsubscribe();
    this.generationSub?.unsubscribe();
    this.audioPlayback.dispose();
  }

  protected toggleSettings(): void {
    this.settingsOpen.update(v => !v);
  }

  protected toggleScrollWhilePlaying(): void {
    this.audioPlayback.scrollWhilePlaying.update(v => !v);
  }

  protected scrollToScene(index: number): void {
    this.activeSceneIndex.set(index);
    const el = document.getElementById(`scene-${index}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected goHome(): void {
    this.statusSub?.unsubscribe();
    this.generationSub?.unsubscribe();
    this.router.navigate(['/']);
  }

  protected async startGeneration(): Promise<void> {
    const spId = this.screenplayId();
    if (!spId || this.isGenerating()) return;

    this.generationStatus.set('starting');
    this.generationError.set(null);
    this.nodeStatuses.set(new Map());

    try {
      const runId = await this.contentGenService.startRun(spId);
      this.generationRunId.set(runId);
      await this.contentGenService.completeVoiceDetermination(runId);
      this.generationStatus.set('running');
      this.subscribeToGeneration(runId);
      if (this.activeTab() === 'generation') {
        void this.loadGenerationRuns();
      }
    } catch (err) {
      this.generationError.set(err instanceof Error ? err.message : 'Failed to start generation');
      this.generationStatus.set('failed');
    }
  }

  protected async continueGeneration(): Promise<void> {
    const runId = this.generationRunId();
    if (!runId || this.isGenerating()) return;

    this.generationStatus.set('starting');
    this.generationError.set(null);

    try {
      await this.contentGenService.replayRun(runId);
      this.generationStatus.set('running');
      this.subscribeToGeneration(runId);
      if (this.activeTab() === 'generation') {
        void this.loadGenerationRuns();
      }
    } catch (err) {
      this.generationError.set(err instanceof Error ? err.message : 'Failed to continue generation');
      this.generationStatus.set('failed');
    }
  }

  protected async regenerateNode(nodeId: string): Promise<void> {
    this.nodeStatuses.update(prev => {
      const next = new Map(prev);
      const existing = next.get(nodeId);
      if (existing) {
        next.set(nodeId, { ...existing, status: 'Ready', lastError: '' });
      }
      return next;
    });

    try {
      await this.contentGenService.regenerateNode(nodeId);
      const runId = this.generationRunId();
      if (runId && !this.isGenerating()) {
        this.generationStatus.set('running');
        this.subscribeToGeneration(runId);
      }
    } catch (err) {
      console.error('Regeneration failed:', err);
    }
  }

  protected toggleProgressPopup(): void {
    this.progressPopupOpen.update(v => !v);
  }

  protected getElementStatus(elementId: string): ElementGenStatus {
    return this.elementStatuses().get(elementId) ?? 'idle';
  }

  protected getNodeIdForElement(elementId: string): string | undefined {
    return this.elementToNodeId().get(elementId);
  }

  protected playAll(): void {
    const queue = this.playbackQueue();
    if (queue.length > 0) {
      this.audioPlayback.playAll(queue);
    }
  }

  protected playNode(elementId: string): void {
    if (this.audioPlayback.isElementActive(elementId)) {
      this.audioPlayback.togglePause();
      return;
    }
    const nodeId = this.elementToNodeId().get(elementId);
    if (!nodeId) return;
    const node = this.nodeStatuses().get(nodeId);
    if (!node || node.status !== 'Succeeded') return;

    const queue = this.playbackQueue();
    const startIndex = queue.findIndex(q => q.nodeId === nodeId);
    if (startIndex >= 0) {
      this.audioPlayback.playFrom(queue, startIndex);
    } else {
      this.audioPlayback.playNode(nodeId, node.elementIds);
    }
  }

  protected playBatchNode(nodeId: string): void {
    if (this.audioPlayback.isNodeActive(nodeId)) {
      this.audioPlayback.togglePause();
      return;
    }
    const node = this.nodeStatuses().get(nodeId);
    if (!node || node.status !== 'Succeeded') return;

    const queue = this.playbackQueue();
    const startIndex = queue.findIndex(q => q.nodeId === nodeId);
    if (startIndex >= 0) {
      this.audioPlayback.playFrom(queue, startIndex);
    } else {
      this.audioPlayback.playNode(nodeId, node.elementIds);
    }
  }

  protected togglePlayPause(): void {
    if (this.audioPlayback.isPlaying()) {
      this.audioPlayback.togglePause();
    } else if (this.audioPlayback.state() === 'paused') {
      this.audioPlayback.togglePause();
    } else {
      this.playAll();
    }
  }

  protected stopPlayback(): void {
    this.audioPlayback.stop();
  }

  protected canPlayElement(elementId: string): boolean {
    return this.getElementStatus(elementId) === 'succeeded';
  }

  protected playNodeDetail(nodeId: string, elementIds: string[]): void {
    this.audioPlayback.playNode(nodeId, elementIds);
  }

  protected async switchTab(tab: ActiveTab): Promise<void> {
    this.activeTab.set(tab);
    if (tab === 'generation') {
      await this.loadGenerationRuns();
    }
  }

  protected async loadGenerationRuns(): Promise<void> {
    const spId = this.screenplayId();
    if (!spId || this.generationRunsLoading()) return;
    this.generationRunsLoading.set(true);
    try {
      const runs = await this.contentGenService.listRunsForScreenplay(spId);
      this.generationRuns.set(runs);
    } catch {
      // Silently fail — no runs yet is fine
      this.generationRuns.set([]);
    } finally {
      this.generationRunsLoading.set(false);
    }
  }

  protected async selectRun(runId: string): Promise<void> {
    if (this.selectedRunDetailsLoading()) return;
    // Toggle off if already selected
    if (this.selectedRunDetails()?.runId === runId) {
      this.selectedRunDetails.set(null);
      return;
    }
    this.selectedRunDetails.set(null);
    this.nodeStatusFilter.set('all');
    this.collapsedScenes.set(new Set());
    this.selectedRunDetailsLoading.set(true);
    try {
      const details = await this.contentGenService.getRunDetails(runId);
      this.selectedRunDetails.set(details);
    } catch (err) {
      console.error('Failed to load run details:', err);
    } finally {
      this.selectedRunDetailsLoading.set(false);
    }
  }

  protected formatRunDate(isoDate: string): string {
    if (!isoDate) return '';
    try {
      return new Date(isoDate).toLocaleString();
    } catch {
      return isoDate;
    }
  }

  protected getRunStatusColor(status: string): string {
    switch (status) {
      case 'Completed': return 'text-green-500';
      case 'Failed': return 'text-destructive';
      case 'Running': return 'text-primary';
      case 'Cancelled': return 'text-muted-foreground';
      default: return 'text-muted-foreground';
    }
  }

  protected getNodeStatusColor(status: string): string {
    switch (status) {
      case 'Succeeded': return 'text-green-500';
      case 'Failed': return 'text-destructive';
      case 'Running': return 'text-primary';
      case 'Blocked': return 'text-orange-500';
      default: return 'text-muted-foreground';
    }
  }

  protected getContentNodes(details: RunDetailsInfo): NodeDetailInfo[] {
    return details.nodes.filter(n => n.kind !== 'PreGenerationAnalysis');
  }

  protected readonly nodeFilterOptions: { value: NodeFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'succeeded', label: 'Succeeded' },
    { value: 'failed', label: 'Failed' },
    { value: 'pending', label: 'Pending' },
  ];

  protected setNodeFilter(filter: NodeFilter): void {
    this.nodeStatusFilter.set(filter);
  }

  protected toggleSceneCollapse(sceneId: string): void {
    this.collapsedScenes.update(prev => {
      const next = new Set(prev);
      if (next.has(sceneId)) {
        next.delete(sceneId);
      } else {
        next.add(sceneId);
      }
      return next;
    });
  }

  protected isSceneCollapsed(sceneId: string): boolean {
    return this.collapsedScenes().has(sceneId);
  }

  protected getSceneBlocks(scene: ScreenplayScene): GenBlock[] {
    const elToNode = this.elementToNodeId();
    const nodeMap = this.nodeStatuses();
    const blocks: GenBlock[] = [];
    let currentBlock: GenBlock | null = null;

    for (let j = 0; j < scene.elements.length; j++) {
      const el = scene.elements[j];
      const nodeId = elToNode.get(el.id);
      const prev = j > 0 ? scene.elements[j - 1] : null;
      const showCharCue = !!(el.character && (el.type === 'Dialogue' || el.type === 'VoiceOver') &&
        (!prev || prev.character !== el.character ||
         (prev.type !== 'Dialogue' && prev.type !== 'VoiceOver' && prev.type !== 'Parenthetical')));

      if (nodeId && currentBlock?.nodeId === nodeId) {
        currentBlock.elements.push({ element: el, showCharCue, isVoiceOver: el.type === 'VoiceOver' });
      } else {
        currentBlock = {
          nodeId,
          status: nodeId ? this.nodeStatusToElementStatus(nodeMap.get(nodeId)?.status ?? '') : 'idle',
          kind: nodeId ? nodeMap.get(nodeId)?.kind : undefined,
          elements: [{ element: el, showCharCue, isVoiceOver: el.type === 'VoiceOver' }],
        };
        blocks.push(currentBlock);
      }
    }
    return blocks;
  }

  private async checkExistingGeneration(screenplayId: string): Promise<void> {
    try {
      const result = await this.contentGenService.getLatestRunForScreenplay(screenplayId);
      if (!result) return;

      this.generationRunId.set(result.runId);
      const nodeMap = new Map<string, NodeStatusInfo>();
      for (const node of result.nodes) {
        nodeMap.set(node.nodeId, node);
      }
      this.nodeStatuses.set(nodeMap);

      if (result.runStatus === 'Completed') {
        this.generationStatus.set('completed');
      } else if (result.runStatus === 'Failed') {
        this.generationStatus.set('failed');
        this.generationError.set(result.errorMessage || 'Generation failed');
      } else if (result.runStatus === 'Running' || result.runStatus === 'Pending') {
        this.generationStatus.set('running');
        this.subscribeToGeneration(result.runId);
      }
    } catch {
      // No previous generation — that's fine
    }
  }

  private subscribeToGeneration(runId: string): void {
    this.generationSub?.unsubscribe();
    this.generationSub = this.contentGenService.streamRunStatus(runId).subscribe({
      next: (evt: RunStatusUpdateEvent) => this.handleGenerationUpdate(evt),
      error: (err: Error) => {
        this.generationError.set(`Connection lost: ${err.message}`);
        if (this.generationStatus() === 'running') {
          this.generationStatus.set('failed');
        }
      },
      complete: () => {
        if (this.generationStatus() === 'running') {
          const progress = this.generationProgress();
          this.generationStatus.set(progress.failed > 0 ? 'failed' : 'completed');
        }
      },
    });
  }

  private handleGenerationUpdate(evt: RunStatusUpdateEvent): void {
    this.nodeStatuses.update(prev => {
      const next = new Map(prev);
      for (const node of evt.nodes) {
        next.set(node.nodeId, node);
      }
      return next;
    });

    this.syncGenerationRunRowFromStream(evt);
    this.mergeSelectedRunDetailsFromStream(evt);

    if (evt.runStatus === 'Completed') {
      this.generationStatus.set('completed');
    } else if (evt.runStatus === 'Failed') {
      this.generationError.set(evt.errorMessage || 'Generation failed');
      this.generationStatus.set('failed');
    }
  }

  /** Keep Generation tab run list in sync with live `nodeStatuses` (same source as the sidebar count). */
  private syncGenerationRunRowFromStream(evt: RunStatusUpdateEvent): void {
    const runId = evt.runId;
    const p = this.generationProgress();
    this.generationRuns.update(list => {
      const idx = list.findIndex(r => r.runId === runId);
      if (idx < 0) return list;
      const cur = list[idx];
      const next = [...list];
      next[idx] = {
        ...cur,
        status: evt.runStatus || cur.status,
        phase: evt.phase || cur.phase,
        totalNodes: p.total,
        succeededNodes: p.succeeded,
        failedNodes: p.failed,
      };
      return next;
    });
  }

  /** When a run is expanded, merge streaming node updates into details so filters and rows stay current. */
  private mergeSelectedRunDetailsFromStream(evt: RunStatusUpdateEvent): void {
    this.selectedRunDetails.update(details => {
      if (!details || details.runId !== evt.runId) return details;
      const byId = new Map(details.nodes.map(n => [n.nodeId, n]));
      for (const u of evt.nodes) {
        const cur = byId.get(u.nodeId);
        if (cur) {
          byId.set(u.nodeId, {
            ...cur,
            status: u.status,
            retryCount: u.retryCount,
            lastError: u.lastError,
          });
        }
      }
      return {
        ...details,
        phase: evt.phase || details.phase,
        status: evt.runStatus || details.status,
        errorMessage: evt.errorMessage ?? details.errorMessage,
        nodes: details.nodes.map(n => byId.get(n.nodeId) ?? n),
      };
    });
  }

  private nodeStatusToElementStatus(status: string): ElementGenStatus {
    switch (status) {
      case 'Succeeded': return 'succeeded';
      case 'Failed': return 'failed';
      case 'Running': return 'running';
      case 'Pending':
      case 'Ready':
      case 'NeedsRetry': return 'pending';
      default: return 'idle';
    }
  }

  private subscribeToStatus(blobName: string): void {
    this.statusSub?.unsubscribe();
    this.statusSub = this.screenplayService.streamImportStatus(blobName).subscribe({
      next: (evt: ImportEvent) => this.handleImportEvent(evt),
      error: (err: Error) => {
        this.error.set(`Connection lost: ${err.message}`);
        if (!this.screenplay()) {
          this.view.set('error');
        }
      },
      complete: () => {
        this.importPhase.set('Import complete');
        this.checkAllChunksFailed();
      },
    });
  }

  private async handleImportEvent(evt: ImportEvent): Promise<void> {
    switch (evt.kind) {
      case 'BlobUploaded':
        this.importPhase.set('Validating…');
        break;
      case 'ValidationFailed':
        this.error.set(evt.errorMessage || 'PDF validation failed');
        this.view.set('error');
        break;
      case 'ValidationPassed':
        this.screenplayId.set(evt.screenplayId);
        this.importPhase.set('Processing…');
        break;
      case 'ChunkSplitDone':
        if (evt.totalPages > 0) this.totalPages.set(evt.totalPages);
        this.updateChunks(evt.startPage, evt.endPage, 'processing');
        this.importPhase.set('Processing…');
        break;
      case 'ChunkProcessDone':
        if (evt.totalPages > 0) this.totalPages.set(evt.totalPages);
        this.updateChunks(evt.startPage, evt.endPage, 'done');
        if (evt.screenplayId || this.screenplayId()) {
          await this.fetchScreenplayData(evt.screenplayId || this.screenplayId()!);
        }
        break;
      case 'ChunkProcessFailed':
        this.updateChunks(evt.startPage, evt.endPage, 'failed');
        this.checkAllChunksFailed();
        break;
      case 'ImportFailed':
        this.error.set(evt.errorMessage || 'Import failed');
        if (!this.screenplay()) this.view.set('error');
        break;
    }
  }

  private updateChunks(startPage: number, endPage: number, status: ChunkPageRange['status']): void {
    this.chunks.update(prev => {
      const existing = prev.find(c => c.startPage === startPage);
      if (existing) {
        const effectiveEndPage = endPage > 0 ? endPage : existing.endPage;
        return prev.map(c => (c.startPage === startPage ? { ...c, endPage: effectiveEndPage, status } : c));
      }
      return [...prev, { startPage, endPage, status }].sort((a, b) => a.startPage - b.startPage);
    });
  }

  private checkAllChunksFailed(): void {
    const c = this.chunks();
    if (c.length > 0 && c.every(ch => ch.status === 'failed') && !this.screenplay() && this.view() === 'importing') {
      this.statusSub?.unsubscribe();
      this.statusSub = null;
      this.chunks.set([]);
      this.screenplayId.set(null);
      this.totalPages.set(0);
      this.error.set('All pages failed to process. Please try a different PDF.');
      this.view.set('error');
    }
  }

  private async fetchScreenplayData(id: string): Promise<void> {
    try {
      const data = await this.screenplayService.getScreenplay(id);
      this.screenplay.set(data);
      if (data.totalPages > 0) this.totalPages.set(data.totalPages);
      this.view.set('viewing');
    } catch (err) {
      console.error('Failed to fetch screenplay data:', err);
      if (!this.screenplay()) {
        this.error.set('Failed to load screenplay');
        this.view.set('error');
      }
    }
  }
}
