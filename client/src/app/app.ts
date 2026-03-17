import { Component, computed, signal, OnDestroy, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { UploadService } from '../lib/services/upload.service';
import {
  ScreenplayService,
  ImportEvent,
  ScreenplayData,
  ScreenplayScene,
} from '../lib/services/screenplay.service';
import { ThemeService } from '../lib/services/theme.service';

type ViewState = 'upload' | 'importing' | 'viewing';

interface ChunkPageRange {
  startPage: number;
  endPage: number;
  status: 'pending' | 'splitting' | 'processing' | 'done' | 'failed';
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App implements OnInit, OnDestroy {
  protected readonly view = signal<ViewState>('upload');
  protected readonly dragOver = signal(false);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly importPhase = signal('Uploading…');
  protected readonly screenplayId = signal<string | null>(null);
  protected readonly totalPages = signal(0);
  protected readonly chunks = signal<ChunkPageRange[]>([]);
  protected readonly screenplay = signal<ScreenplayData | null>(null);
  protected readonly activeSceneIndex = signal(0);

  protected readonly processedPages = computed(() => {
    const done = this.chunks().filter(c => c.status === 'done');
    return done.reduce((sum, c) => sum + (c.endPage - c.startPage + 1), 0);
  });

  protected readonly scenes = computed(() => this.screenplay()?.scenes ?? []);

  protected readonly isAllDone = computed(() => {
    const c = this.chunks();
    return c.length > 0 && c.every(ch => ch.status === 'done' || ch.status === 'failed');
  });

  protected readonly pageRangeForScene = (scene: ScreenplayScene): string => {
    return `p.${scene.page}`;
  };

  private statusSub: Subscription | null = null;

  protected readonly themeService: ThemeService;

  constructor(
    private readonly uploadService: UploadService,
    private readonly screenplayService: ScreenplayService,
    themeService: ThemeService,
  ) {
    this.themeService = themeService;
  }

  ngOnInit(): void {
    this.themeService.init();
  }

  ngOnDestroy(): void {
    this.statusSub?.unsubscribe();
  }

  protected onDragOver(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(true);
  }

  protected onDragLeave(): void {
    this.dragOver.set(false);
  }

  protected onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(false);
    const file = e.dataTransfer?.files[0];
    if (file) this.startUpload(file);
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.startUpload(file);
    input.value = '';
  }

  protected isPageAvailable(page: number): boolean {
    return this.chunks().some(
      c => c.status === 'done' && page >= c.startPage && page <= c.endPage,
    );
  }

  protected getChunkStatus(page: number): string {
    const chunk = this.chunks().find(c => page >= c.startPage && page <= c.endPage);
    return chunk?.status ?? 'pending';
  }

  protected scrollToScene(index: number): void {
    this.activeSceneIndex.set(index);
    const el = document.getElementById(`scene-${index}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected resetToUpload(): void {
    this.statusSub?.unsubscribe();
    this.statusSub = null;
    this.view.set('upload');
    this.error.set(null);
    this.screenplay.set(null);
    this.screenplayId.set(null);
    this.totalPages.set(0);
    this.chunks.set([]);
    this.activeSceneIndex.set(0);
  }

  private async startUpload(file: File): Promise<void> {
    if (file.type && file.type !== 'application/pdf') {
      this.error.set('Only PDF files are accepted.');
      return;
    }
    if (file.size > 50 * 1024 * 1024) {
      this.error.set('File exceeds 50 MB limit.');
      return;
    }

    this.error.set(null);
    this.uploading.set(true);
    this.view.set('importing');
    this.importPhase.set('Uploading…');

    try {
      const { blobName } = await this.uploadService.uploadPdf(file);
      this.importPhase.set('Validating…');
      this.subscribeToStatus(blobName);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Upload failed');
      this.view.set('upload');
    } finally {
      this.uploading.set(false);
    }
  }

  private subscribeToStatus(blobName: string): void {
    this.statusSub?.unsubscribe();

    this.statusSub = this.screenplayService.streamImportStatus(blobName).subscribe({
      next: (evt: ImportEvent) => this.handleImportEvent(evt),
      error: (err: Error) => {
        this.error.set(`Connection lost: ${err.message}`);
        // Don't reset to upload if we already have data
        if (!this.screenplay()) {
          this.view.set('upload');
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
        this.view.set('upload');
        break;

      case 'ValidationPassed':
        this.screenplayId.set(evt.screenplayId);
        this.importPhase.set('Processing…');
        break;

      case 'ChunkSplitDone':
        if (evt.totalPages > 0) {
          this.totalPages.set(evt.totalPages);
        }
        this.updateChunks(evt.startPage, evt.endPage, 'processing');
        this.importPhase.set('Processing…');
        break;

      case 'ChunkProcessDone':
        if (evt.totalPages > 0) {
          this.totalPages.set(evt.totalPages);
        }
        this.updateChunks(evt.startPage, evt.endPage, 'done');
        // Fetch screenplay data when pages are processed
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
        if (!this.screenplay()) {
          this.view.set('upload');
        }
        break;
    }
  }

  private updateChunks(startPage: number, endPage: number, status: ChunkPageRange['status']): void {
    this.chunks.update(prev => {
      const existing = prev.find(c => c.startPage === startPage);
      if (existing) {
        const effectiveEndPage = endPage > 0 ? endPage : existing.endPage;
        return prev.map(c => c.startPage === startPage ? { ...c, endPage: effectiveEndPage, status } : c);
      }
      return [...prev, { startPage, endPage, status }].sort((a, b) => a.startPage - b.startPage);
    });
  }

  private checkAllChunksFailed(): void {
    const c = this.chunks();
    if (
      c.length > 0 &&
      c.every(ch => ch.status === 'failed') &&
      !this.screenplay() &&
      this.view() === 'importing'
    ) {
      this.statusSub?.unsubscribe();
      this.statusSub = null;
      this.chunks.set([]);
      this.screenplayId.set(null);
      this.totalPages.set(0);
      this.error.set('All pages failed to process. Please try a different PDF.');
      this.view.set('upload');
    }
  }

  private async fetchScreenplayData(id: string): Promise<void> {
    try {
      const data = await this.screenplayService.getScreenplay(id);
      this.screenplay.set(data);
      if (data.totalPages > 0) {
        this.totalPages.set(data.totalPages);
      }
      if (this.view() === 'importing') {
        this.view.set('viewing');
      }
    } catch (err) {
      console.error('Failed to fetch screenplay data:', err);
    }
  }
}
