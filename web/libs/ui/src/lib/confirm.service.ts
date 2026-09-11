import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

export interface ConfirmRequest extends Required<Omit<ConfirmOptions, 'destructive'>> {
  destructive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly request = signal<ConfirmRequest | null>(null);
  private resolver: ((value: boolean) => void) | null = null;

  confirm(options: ConfirmOptions): Promise<boolean> {
    this.resolver?.(false);
    return new Promise(resolve => {
      this.resolver = resolve;
      this.request.set({
        title: options.title,
        message: options.message,
        confirmLabel: options.confirmLabel ?? 'Bekräfta',
        cancelLabel: options.cancelLabel ?? 'Avbryt',
        destructive: options.destructive ?? false
      });
    });
  }

  resolve(value: boolean): void {
    this.resolver?.(value);
    this.resolver = null;
    this.request.set(null);
  }
}
