import { Injectable } from '@angular/core';

export interface OfflineDraft {
  localId: string;
  serverId: string | null;
  payload: Record<string, unknown>;
  updatedAt: string;
  syncStatus: 'pending' | 'synced' | 'error';
}

const DB_NAME = 'equine-drafts';
const STORE = 'drafts';

@Injectable({ providedIn: 'root' })
export class DraftStoreService {
  private dbPromise: Promise<IDBDatabase> | null = null;

  private open(): Promise<IDBDatabase> {
    if (this.dbPromise) return this.dbPromise;
    this.dbPromise = new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 1);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains(STORE)) {
          db.createObjectStore(STORE, { keyPath: 'localId' });
        }
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    return this.dbPromise;
  }

  async put(draft: OfflineDraft): Promise<void> {
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(draft);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  }

  async get(localId: string): Promise<OfflineDraft | undefined> {
    const db = await this.open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).get(localId);
      req.onsuccess = () => resolve(req.result as OfflineDraft | undefined);
      req.onerror = () => reject(req.error);
    });
  }

  async getByServerId(serverId: string): Promise<OfflineDraft | undefined> {
    const all = await this.getAll();
    return all.find(d => d.serverId === serverId);
  }

  async getAll(): Promise<OfflineDraft[]> {
    const db = await this.open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).getAll();
      req.onsuccess = () => resolve((req.result as OfflineDraft[]) ?? []);
      req.onerror = () => reject(req.error);
    });
  }

  async pending(): Promise<OfflineDraft[]> {
    const all = await this.getAll();
    return all.filter(d => d.syncStatus !== 'synced');
  }
}
