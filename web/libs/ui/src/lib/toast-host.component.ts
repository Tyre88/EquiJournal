import { Component } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'ej-toast-host',
  standalone: true,
  template: `
    <div class="toast-host" aria-live="polite" aria-relevant="additions">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast-' + toast.kind" role="status">
          <span>{{ toast.message }}</span>
          <button type="button" class="btn-ghost btn-sm" (click)="toasts.dismiss(toast.id)" aria-label="Stäng">×</button>
        </div>
      }
    </div>
  `
})
export class EjToastHostComponent {
  constructor(readonly toasts: ToastService) {}
}
