import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'storybook-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = signal<Theme>(this.resolve());

  toggle(): void {
    const next = this.theme() === 'dark' ? 'light' : 'dark';
    this.apply(next);
  }

  init(): void {
    if (this.isBrowser) {
      this.apply(this.theme());
    }
  }

  private resolve(): Theme {
    if (!this.isBrowser) return 'dark';
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }

  private apply(theme: Theme): void {
    this.theme.set(theme);
    if (!this.isBrowser) return;
    localStorage.setItem(STORAGE_KEY, theme);
    document.documentElement.setAttribute('data-theme', theme);
  }
}
