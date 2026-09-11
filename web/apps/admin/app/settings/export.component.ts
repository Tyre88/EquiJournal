import { Component, inject, signal } from '@angular/core';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';

@Component({
  selector: 'app-export-settings',
  standalone: true,
  imports: [EjPageHeaderComponent],
  template: `
    <ej-page-header title="Exportera arkiv" subtitle="Fullständig JSON+filer och journaler som PDF per häst." />
    <div class="card">
      <p>Ladda ner ett återimporterbart arkiv (ägare, hästar, bokningar, journaler, ändringar och bilagor) eller läsbara journal-PDF:er organiserade per häst.</p>
      <div class="actions">
        <button type="button" class="btn-primary" [disabled]="busy()" (click)="download('archive')">
          {{ busy() === 'archive' ? 'Skapar arkiv…' : 'Ladda ner fullständigt arkiv' }}
        </button>
        <button type="button" class="btn-secondary" [disabled]="busy()" (click)="download('journals-pdf')">
          {{ busy() === 'journals-pdf' ? 'Skapar PDF…' : 'Ladda ner journal-PDF:er' }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    .actions { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 1rem; }
  `]
})
export class ExportSettingsComponent {
  private api = inject(Api);
  private toast = inject(ToastService);
  busy = signal<string | null>(null);

  download(kind: 'archive' | 'journals-pdf') {
    this.busy.set(kind);
    this.api.download(`/api/app/export/${kind}`).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = kind === 'archive' ? 'hastjournal-arkiv.zip' : 'hastjournal-journaler.zip';
        a.click();
        URL.revokeObjectURL(url);
        this.busy.set(null);
        this.toast.success('Exporten är klar.');
      },
      error: () => {
        this.busy.set(null);
        this.toast.error('Exporten misslyckades.');
      }
    });
  }
}
