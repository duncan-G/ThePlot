import {
  Component,
  signal,
  computed,
  OnInit,
  OnDestroy,
  ElementRef,
  viewChild,
  ChangeDetectionStrategy,
  inject,
  PLATFORM_ID,
  effect,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  ScreenplayService,
  ScreenplaySummary,
  ImportEvent,
} from '../../lib/services/screenplay.service';
import { UploadService } from '../../lib/services/upload.service';
import { ThemeService } from '../../lib/services/theme.service';

@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly screenplayService = inject(ScreenplayService);
  private readonly uploadService = inject(UploadService);
  protected readonly themeService = inject(ThemeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly screenplays = signal<ScreenplaySummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadingMore = signal(false);
  protected readonly nextPageToken = signal('');
  protected readonly error = signal<string | null>(null);

  protected readonly dragOver = signal(false);
  protected readonly uploading = signal(false);
  protected readonly uploadError = signal<string | null>(null);
  protected readonly importPhase = signal('');
  protected readonly importing = signal(false);
  protected readonly settingsOpen = signal(false);
  protected readonly openMenuId = signal<string | null>(null);
  protected readonly deleteTarget = signal<ScreenplaySummary | null>(null);
  protected readonly deleting = signal(false);

  protected readonly hasMore = computed(() => this.nextPageToken().length > 0);
  protected readonly hasScreenplays = computed(() => this.screenplays().length > 0);
  protected readonly showEmptyState = computed(() => !this.loading() && !this.hasScreenplays());

  private scrollSentinel = viewChild<ElementRef<HTMLDivElement>>('scrollSentinel');
  private statusSub: Subscription | null = null;

  constructor() {
    if (this.isBrowser) {
      effect(onCleanup => {
        const sentinel = this.scrollSentinel()?.nativeElement;
        if (!sentinel) return;

        const observer = new IntersectionObserver(
          entries => {
            if (entries[0]?.isIntersecting && this.hasMore() && !this.loadingMore()) {
              this.loadMore();
            }
          },
          { rootMargin: '200px' },
        );
        observer.observe(sentinel);

        onCleanup(() => observer.disconnect());
      });
    }
  }

  ngOnInit(): void {
    this.themeService.init();
    if (this.isBrowser) {
      this.loadScreenplays();
    }
  }

  ngOnDestroy(): void {
    this.statusSub?.unsubscribe();
  }

  private async loadScreenplays(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.screenplayService.listScreenplays(20);
      this.screenplays.set(page.items);
      this.nextPageToken.set(page.nextPageToken);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load screenplays');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadMore(): Promise<void> {
    if (!this.hasMore() || this.loadingMore()) return;
    this.loadingMore.set(true);
    try {
      const page = await this.screenplayService.listScreenplays(20, this.nextPageToken());
      this.screenplays.update(prev => [...prev, ...page.items]);
      this.nextPageToken.set(page.nextPageToken);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load more');
    } finally {
      this.loadingMore.set(false);
    }
  }

  protected toggleSettings(): void {
    this.settingsOpen.update(v => !v);
  }

  protected openScreenplay(id: string): void {
    this.router.navigate(['/screenplays', id]);
  }

  protected toggleMenu(id: string): void {
    this.openMenuId.update(v => (v === id ? null : id));
  }

  protected closeMenu(): void {
    this.openMenuId.set(null);
  }

  protected confirmDelete(sp: ScreenplaySummary): void {
    this.openMenuId.set(null);
    this.deleteTarget.set(sp);
  }

  protected cancelDelete(): void {
    this.deleteTarget.set(null);
  }

  protected async executeDelete(): Promise<void> {
    const target = this.deleteTarget();
    if (!target || this.deleting()) return;

    this.deleting.set(true);
    try {
      await this.screenplayService.deleteScreenplay(target.id);
      this.screenplays.update(list => list.filter(s => s.id !== target.id));
      this.deleteTarget.set(null);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to delete screenplay');
      this.deleteTarget.set(null);
    } finally {
      this.deleting.set(false);
    }
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

  protected async startUpload(file: File): Promise<void> {
    if (file.type && file.type !== 'application/pdf') {
      this.uploadError.set('Only PDF files are accepted.');
      return;
    }
    if (file.size > 50 * 1024 * 1024) {
      this.uploadError.set('File exceeds 50 MB limit.');
      return;
    }

    this.uploadError.set(null);
    this.uploading.set(true);
    this.importing.set(true);
    this.importPhase.set('Uploading…');

    try {
      const { blobName } = await this.uploadService.uploadPdf(file);
      this.importPhase.set('Processing…');
      this.subscribeToStatus(blobName);
    } catch (err) {
      this.uploadError.set(err instanceof Error ? err.message : 'Upload failed');
      this.importing.set(false);
    } finally {
      this.uploading.set(false);
    }
  }

  private subscribeToStatus(blobName: string): void {
    this.statusSub?.unsubscribe();
    this.statusSub = this.screenplayService.streamImportStatus(blobName).subscribe({
      next: (evt: ImportEvent) => {
        switch (evt.kind) {
          case 'ValidationPassed':
            this.importPhase.set('Processing…');
            break;
          case 'ValidationFailed':
            this.uploadError.set(evt.errorMessage || 'PDF validation failed');
            this.importing.set(false);
            break;
          case 'ChunkProcessDone':
            if (evt.screenplayId) {
              this.importing.set(false);
              this.importPhase.set('');
              this.router.navigate(['/screenplays', evt.screenplayId], {
                queryParams: { blob: blobName },
              });
            }
            break;
          case 'ImportFailed':
            this.uploadError.set(evt.errorMessage || 'Import failed');
            this.importing.set(false);
            break;
        }
      },
      error: (err: Error) => {
        this.uploadError.set(`Connection lost: ${err.message}`);
        this.importing.set(false);
      },
    });
  }

  protected formatDate(isoDate: string): string {
    try {
      return new Date(isoDate).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
      });
    } catch {
      return '';
    }
  }
}
