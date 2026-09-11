import { Injectable, inject } from '@angular/core';
import { Api } from './api';
import { SyncStatusService } from './sync-status.service';

export interface QueuedAttachment {
  localId: string;
  journalId: string;
  file: Blob;
  fileName: string;
  mimeType: string;
  retryCount: number;
  status: 'pending' | 'uploading' | 'error';
}

const DB_NAME = 'equine-offline';

@Injectable({ providedIn: 'root' })
export class AttachmentQueueService {
  private api = inject(Api);
  private sync = inject(SyncStatusService);
  private dbPromise: Promise<IDBDatabase> | null = null;

  private open(): Promise<IDBDatabase> {
    if (this.dbPromise) return this.dbPromise;
    this.dbPromise = new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 2);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains('attachments')) {
          db.createObjectStore('attachments', { keyPath: 'localId' });
        }
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    return this.dbPromise;
  }

  async enqueue(journalId: string, file: File): Promise<string> {
    const localId = crypto.randomUUID();
    const item: QueuedAttachment = {
      localId,
      journalId,
      file,
      fileName: file.name,
      mimeType: file.type,
      retryCount: 0,
      status: 'pending'
    };
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction('attachments', 'readwrite');
      tx.objectStore('attachments').put(item);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
    return localId;
  }

  async getAll(): Promise<QueuedAttachment[]> {
    const db = await this.open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('attachments', 'readonly');
      const req = tx.objectStore('attachments').getAll();
      req.onsuccess = () => resolve(req.result as QueuedAttachment[]);
      req.onerror = () => reject(req.error);
    });
  }

  async remove(localId: string): Promise<void> {
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction('attachments', 'readwrite');
      tx.objectStore('attachments').delete(localId);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  }

  async flush(): Promise<void> {
    if (!navigator.onLine) return;
    const pending = (await this.getAll()).filter(a => a.status !== 'uploading');
    if (pending.length === 0) return;

    this.sync.setSyncing(pending.length);
    for (const item of pending) {
      try {
        const file = new File([item.file], item.fileName, { type: item.mimeType });
        await new Promise<void>((resolve, reject) => {
          this.api.upload(`/api/app/journals/${item.journalId}/attachments`, file).subscribe({
            next: () => resolve(),
            error: err => reject(err)
          });
        });
        await this.remove(item.localId);
      } catch {
        item.retryCount++;
        item.status = 'error';
        if (item.retryCount < 5) {
          const db = await this.open();
          await new Promise<void>((resolve, reject) => {
            const tx = db.transaction('attachments', 'readwrite');
            tx.objectStore('attachments').put(item);
            tx.oncomplete = () => resolve();
            tx.onerror = () => reject(tx.error);
          });
        }
      }
    }
    this.sync.setIdle();
  }
}
