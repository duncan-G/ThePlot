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
} from '../../lib/services/screenplay.service';
import { ThemeService } from '../../lib/services/theme.service';

type ViewState = 'importing' | 'viewing' | 'error';

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

  protected readonly processedPages = computed(() => {
    const done = this.chunks().filter(c => c.status === 'done');
    return done.reduce((sum, c) => sum + (c.endPage - c.startPage + 1), 0);
  });

  protected readonly scenes = computed(() => this.screenplay()?.scenes ?? []);

  protected readonly isAllDone = computed(() => {
    const c = this.chunks();
    return c.length > 0 && c.every(ch => ch.status === 'done' || ch.status === 'failed');
  });

  private statusSub: Subscription | null = null;

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
  }

  protected scrollToScene(index: number): void {
    this.activeSceneIndex.set(index);
    const el = document.getElementById(`scene-${index}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected goHome(): void {
    this.statusSub?.unsubscribe();
    this.router.navigate(['/']);
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
