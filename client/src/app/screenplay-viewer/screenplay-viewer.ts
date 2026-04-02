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
} from '../../lib/services/screenplay.service';
import {
  ContentGenerationService,
  NodeStatusInfo,
  RunStatusUpdateEvent,
  ElementGenStatus,
} from '../../lib/services/content-generation.service';
import { ThemeService } from '../../lib/services/theme.service';

type ViewState = 'importing' | 'viewing' | 'error';
type GenerationState = 'idle' | 'starting' | 'running' | 'completed' | 'failed';

interface ChunkPageRange {
  startPage: number;
  endPage: number;
  status: 'pending' | 'splitting' | 'processing' | 'done' | 'failed';
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

  // Generation state
  protected readonly generationRunId = signal<string | null>(null);
  protected readonly generationStatus = signal<GenerationState>('idle');
  protected readonly generationError = signal<string | null>(null);
  protected readonly nodeStatuses = signal<Map<string, NodeStatusInfo>>(new Map());
  protected readonly progressPopupOpen = signal(false);

  protected readonly processedPages = computed(() => {
    const done = this.chunks().filter(c => c.status === 'done');
    return done.reduce((sum, c) => sum + (c.endPage - c.startPage + 1), 0);
  });

  protected readonly scenes = computed(() => this.screenplay()?.scenes ?? []);

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
  }

  protected toggleSettings(): void {
    this.settingsOpen.update(v => !v);
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
    } catch (err) {
      this.generationError.set(err instanceof Error ? err.message : 'Failed to start generation');
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

    if (evt.runStatus === 'Completed') {
      this.generationStatus.set('completed');
    } else if (evt.runStatus === 'Failed') {
      this.generationError.set(evt.errorMessage || 'Generation failed');
      this.generationStatus.set('failed');
    }
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
