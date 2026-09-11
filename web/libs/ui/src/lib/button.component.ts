import { Component, input } from '@angular/core';

@Component({
  selector: 'ej-button',
  standalone: true,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [class]="'btn btn-' + variant()"
    >
      <ng-content />
    </button>
  `,
  styles: [`:host { display: inline-flex; } button { width: 100%; }`]
})
export class EjButtonComponent {
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly variant = input<'primary' | 'secondary' | 'danger' | 'ghost'>('primary');
  readonly disabled = input(false);
}
