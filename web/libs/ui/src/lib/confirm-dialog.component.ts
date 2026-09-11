import { Component, HostListener } from '@angular/core';
import { ConfirmService } from './confirm.service';

@Component({
  selector: 'ej-confirm-dialog',
  standalone: true,
  template: `
    @if (confirm.request(); as req) {
      <div class="confirm-overlay" (click)="confirm.resolve(false)">
        <div
          class="confirm-dialog"
          role="alertdialog"
          aria-modal="true"
          [attr.aria-labelledby]="'confirm-title'"
          (click)="$event.stopPropagation()"
        >
          <header>
            <h2 id="confirm-title">{{ req.title }}</h2>
          </header>
          <div class="body">{{ req.message }}</div>
          <footer>
            <button type="button" class="btn-secondary" (click)="confirm.resolve(false)">{{ req.cancelLabel }}</button>
            <button
              type="button"
              [class]="req.destructive ? 'btn-danger' : 'btn-primary'"
              (click)="confirm.resolve(true)"
            >{{ req.confirmLabel }}</button>
          </footer>
        </div>
      </div>
    }
  `
})
export class EjConfirmDialogComponent {
  constructor(readonly confirm: ConfirmService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirm.request()) {
      this.confirm.resolve(false);
    }
  }
}
