import { Component, input } from '@angular/core';
import { EjIconComponent, EjIconName } from './icon.component';

@Component({
  selector: 'ej-empty-state',
  standalone: true,
  imports: [EjIconComponent],
  template: `
    <div class="empty-state">
      @if (icon()) {
        <ej-icon [name]="icon()!" [size]="36" />
      }
      <h2>{{ title() }}</h2>
      @if (description()) {
        <p>{{ description() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: [`
    ej-icon { color: var(--color-primary, #2d5016); margin-bottom: 0.5rem; }
  `]
})
export class EjEmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input('');
  readonly icon = input<EjIconName | null>(null);
}
