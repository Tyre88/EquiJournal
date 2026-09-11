import { Component, input } from '@angular/core';

@Component({
  selector: 'ej-spinner',
  standalone: true,
  template: `
    <span class="spin" [style.width.px]="size()" [style.height.px]="size()" role="status" [attr.aria-label]="label()"></span>
  `,
  styles: [`
    :host { display: inline-flex; }
    .spin {
      border: 2px solid var(--color-border, #ddd4c4);
      border-top-color: var(--color-primary, #2d5016);
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class EjSpinnerComponent {
  readonly size = input(20);
  readonly label = input('Laddar');
}
