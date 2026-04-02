import { Injectable, signal, computed, inject } from '@angular/core';
import { ContentGenerationService, NodeAudioData } from './content-generation.service';

export type PlaybackState = 'idle' | 'loading' | 'playing' | 'paused';

export interface PlaybackQueueItem {
  nodeId: string;
  elementIds: string[];
  sceneId: string;
  kind: string;
}

@Injectable({ providedIn: 'root' })
export class AudioPlaybackService {
  private static readonly PREFETCH_AHEAD = 10;

  private readonly contentGenService = inject(ContentGenerationService);
  private audioElement: HTMLAudioElement | null = null;
  private audioCache = new Map<string, string>();
  private isQueueMode = false;

  readonly state = signal<PlaybackState>('idle');
  readonly activeNodeId = signal<string | null>(null);
  readonly activeElementIds = signal<Set<string>>(new Set());
  readonly queue = signal<PlaybackQueueItem[]>([]);
  readonly queueIndex = signal(-1);

  readonly scrollWhilePlaying = signal(true);
  readonly isPlaying = computed(() => this.state() === 'playing');
  readonly isActive = computed(() => this.state() !== 'idle');

  async playAll(orderedNodes: PlaybackQueueItem[]): Promise<void> {
    if (orderedNodes.length === 0) return;

    this.stop();
    this.isQueueMode = true;
    this.queue.set(orderedNodes);
    this.queueIndex.set(0);
    await this.playCurrentQueueItem();
  }

  async playNode(nodeId: string, elementIds: string[]): Promise<void> {
    this.stop();
    this.isQueueMode = false;
    this.queue.set([{ nodeId, elementIds, sceneId: '', kind: '' }]);
    this.queueIndex.set(0);
    await this.playCurrentQueueItem();
  }

  async playFrom(orderedNodes: PlaybackQueueItem[], startIndex: number): Promise<void> {
    if (orderedNodes.length === 0 || startIndex < 0 || startIndex >= orderedNodes.length) return;
    this.stop();
    this.isQueueMode = true;
    this.queue.set(orderedNodes);
    this.queueIndex.set(startIndex);
    await this.playCurrentQueueItem();
  }

  togglePause(): void {
    if (!this.audioElement) return;

    if (this.state() === 'playing') {
      this.audioElement.pause();
      this.state.set('paused');
    } else if (this.state() === 'paused') {
      this.audioElement.play();
      this.state.set('playing');
    }
  }

  stop(): void {
    if (this.audioElement) {
      this.audioElement.pause();
      this.audioElement.removeAttribute('src');
      this.audioElement.load();
    }
    this.isQueueMode = false;
    this.state.set('idle');
    this.activeNodeId.set(null);
    this.activeElementIds.set(new Set());
    this.queue.set([]);
    this.queueIndex.set(-1);
  }

  isNodeActive(nodeId: string): boolean {
    return this.activeNodeId() === nodeId;
  }

  isElementActive(elementId: string): boolean {
    return this.activeElementIds().has(elementId);
  }

  dispose(): void {
    this.stop();
    this.revokeAllCachedUrls();
  }

  private async playCurrentQueueItem(): Promise<void> {
    const q = this.queue();
    const idx = this.queueIndex();
    if (idx < 0 || idx >= q.length) {
      this.stop();
      return;
    }

    const item = q[idx];
    this.activeNodeId.set(item.nodeId);
    this.activeElementIds.set(new Set(item.elementIds));

    if (!this.audioCache.has(item.nodeId)) {
      this.state.set('loading');
    }

    try {
      const objectUrl = await this.getAudioUrl(item.nodeId);
      if (this.activeNodeId() !== item.nodeId) return;

      if (!this.audioElement) {
        this.audioElement = new Audio();
      }

      this.audioElement.src = objectUrl;
      this.audioElement.onended = () => this.onTrackEnded();
      this.audioElement.onerror = () => this.onTrackError();

      await this.audioElement.play();
      this.state.set('playing');

      if (this.scrollWhilePlaying()) {
        this.scrollToElement(item.elementIds[0]);
      }

      if (this.isQueueMode) {
        this.prefetchUpcoming(idx);
      }
    } catch {
      this.advanceQueue();
    }
  }

  private onTrackEnded(): void {
    this.advanceQueue();
  }

  private onTrackError(): void {
    this.advanceQueue();
  }

  private advanceQueue(): void {
    const next = this.queueIndex() + 1;
    if (next >= this.queue().length) {
      this.stop();
      return;
    }
    this.queueIndex.set(next);
    this.playCurrentQueueItem();
  }

  private prefetchUpcoming(currentIndex: number): void {
    const q = this.queue();
    const end = Math.min(currentIndex + AudioPlaybackService.PREFETCH_AHEAD + 1, q.length);
    for (let i = currentIndex + 1; i < end; i++) {
      const nodeId = q[i].nodeId;
      if (!this.audioCache.has(nodeId)) {
        this.getAudioUrl(nodeId).catch(() => {});
      }
    }
  }

  private async getAudioUrl(nodeId: string): Promise<string> {
    const cached = this.audioCache.get(nodeId);
    if (cached) return cached;

    const data: NodeAudioData = await this.contentGenService.getNodeAudio(nodeId);
    const byteChars = atob(data.audioBase64);
    const bytes = new Uint8Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) {
      bytes[i] = byteChars.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: data.mimeType });
    const url = URL.createObjectURL(blob);
    this.audioCache.set(nodeId, url);
    return url;
  }

  private scrollToElement(elementId: string | undefined): void {
    if (!elementId) return;
    const el = document.querySelector(`[data-element-id="${elementId}"]`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  private revokeAllCachedUrls(): void {
    for (const url of this.audioCache.values()) {
      URL.revokeObjectURL(url);
    }
    this.audioCache.clear();
  }
}
