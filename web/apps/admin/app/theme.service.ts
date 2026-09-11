import { Injectable, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'theme-preference';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preference = signal<ThemePreference>('system');
  readonly resolved = signal<ResolvedTheme>('light');

  constructor() {
    this.preference.set(readPreference());
    this.apply();
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (this.preference() === 'system') this.apply();
    });
  }

  setPreference(value: ThemePreference): void {
    this.preference.set(value);
    localStorage.setItem(STORAGE_KEY, value);
    this.apply();
  }

  private apply(): void {
    const pref = this.preference();
    const resolved: ResolvedTheme = pref === 'system'
      ? (systemPrefersDark() ? 'dark' : 'light')
      : pref;
    this.resolved.set(resolved);
    document.documentElement.setAttribute('data-theme', resolved);
  }
}

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark' || stored === 'system') return stored;
  } catch {
    /* ignore private-mode / blocked storage */
  }
  return 'system';
}

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}
