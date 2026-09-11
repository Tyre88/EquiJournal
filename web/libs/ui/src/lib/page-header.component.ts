import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { EjIconComponent } from './icon.component';

@Component({
  selector: 'ej-page-header',
  standalone: true,
  imports: [EjIconComponent],
  template: `
    <header class="page-header">
      <div>
        @if (backHref()) {
          <a class="btn-ghost btn-sm back" href="#" (click)="goBack($event)">
            <ej-icon name="arrow-left" [size]="18" />
            {{ backLabel() }}
          </a>
        }
        <h1 class="page-title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="page-subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header-actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: [`
    .back { margin: 0 0 0.35rem -0.5rem; color: var(--color-text-muted, #5c6454); }
  `]
})
export class EjPageHeaderComponent {
  constructor(private readonly router: Router) {}

  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly backHref = input<string | readonly unknown[] | null>(null);
  readonly backLabel = input('Tillbaka');

  goBack(event: Event): void {
    event.preventDefault();
    const href = this.backHref();
    if (typeof href === 'string') {
      void this.router.navigateByUrl(href);
    } else if (href) {
      void this.router.navigate(href as never[]);
    }
  }
}
