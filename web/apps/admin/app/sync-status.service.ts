import { Injectable, signal } from '@angular/core';

export type SyncState = 'online' | 'offline' | 'syncing' | 'error';

@Injectable({ providedIn: 'root' })
export class SyncStatusService {
  readonly state = signal<SyncState>('online');
  readonly pendingCount = signal(0);
  readonly message = signal('');

  constructor() {
    if (typeof window !== 'undefined') {
      this.state.set(navigator.onLine ? 'online' : 'offline');
      window.addEventListener('online', () => this.state.set('online'));
      window.addEventListener('offline', () => this.state.set('offline'));
    }
  }

  setSyncing(count = 0): void {
    this.state.set('syncing');
    this.pendingCount.set(count);
  }

  setError(msg: string): void {
    this.state.set('error');
    this.message.set(msg);
  }

  setIdle(): void {
    this.state.set(navigator.onLine ? 'online' : 'offline');
    this.pendingCount.set(0);
    this.message.set('');
  }
}
