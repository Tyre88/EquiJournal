import { Component, HostListener, input, output } from '@angular/core';

@Component({
  selector: 'ej-dialog',
  standalone: true,
  template: `
    @if (open()) {
      <div class="ej-dialog-overlay" (click)="close()">
        <div
          class="ej-dialog"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="title() ? 'ej-dialog-title' : null"
          (click)="$event.stopPropagation()"
        >
          <div class="ej-dialog-header">
            <h2 id="ej-dialog-title">{{ title() }}</h2>
            <button type="button" class="ej-dialog-close" (click)="close()" aria-label="Stäng">×</button>
          </div>
          <div class="ej-dialog-body">
            <ng-content />
          </div>
          @if (footer()) {
            <div class="ej-dialog-footer">
              <ng-content select="[footer]" />
            </div>
          }
        </div>
      </div>
    }
  `
})
export class EjDialogComponent {
  readonly open = input(false);
  readonly title = input('');
  readonly footer = input(false);
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
  }
}
