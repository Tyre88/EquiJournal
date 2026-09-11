import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { ApiErrorService } from '../api-error.service';

type ReportId = 'behandlingar' | 'intakter' | 'uppfoljningar' | 'osignerade' | 'uteblivna' | 'klientaktivitet' | 'bokningskallor';

interface ReportDef {
  id: ReportId;
  title: string;
  hasRange: boolean;
  hasMonths?: boolean;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header title="Rapporter" subtitle="Exportera data som CSV med svensk formatering." />

      <div class="card report-tabs" role="tablist">
        @for (r of reports; track r.id) {
          <button type="button" role="tab" class="tab" [class.active]="active() === r.id" (click)="select(r.id)">
            {{ r.title }}
          </button>
        }
      </div>

      <div class="card">
        @if (current()?.hasRange) {
          <div class="filters">
            <label class="field-label" for="from">Från</label>
            <input id="from" class="input" type="date" [(ngModel)]="from" />
            <label class="field-label" for="to">Till</label>
            <input id="to" class="input" type="date" [(ngModel)]="to" />
          </div>
        }
        @if (current()?.hasMonths) {
          <div class="filters">
            <label class="field-label" for="months">Månader utan bokning</label>
            <input id="months" class="input" type="number" min="1" max="60" [(ngModel)]="months" inputmode="numeric" />
          </div>
        }
        <div class="actions-bar">
          <button type="button" class="btn-primary" (click)="load()" [disabled]="loading()">Visa</button>
          <button type="button" class="btn-secondary" (click)="exportCsv()">Exportera CSV</button>
        </div>
      </div>

      @if (loading()) { <p class="loading">Laddar…</p> }
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }

      @if (rows().length > 0) {
        <div class="card table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                @for (col of columns(); track col) { <th scope="col">{{ col }}</th> }
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track $index) {
                <tr>
                  @for (cell of row; track $index) { <td>{{ cell }}</td> }
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else if (!loading() && loaded()) {
        <p class="muted">Inga rader för valt filter.</p>
      }
    </div>
  `,
  styles: [`
    .report-tabs { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .tab { min-height: var(--tap-min); padding: 0.5rem 0.75rem; border: 1px solid var(--color-border); border-radius: var(--radius-sm); background: var(--color-surface); cursor: pointer; }
    .tab.active { background: var(--color-primary-soft); border-color: var(--color-primary); color: var(--color-primary-text); }
    .filters { display: grid; gap: 0.5rem; margin-bottom: 1rem; max-width: 20rem; }
    .table-wrap { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: var(--text-sm); }
    .data-table th, .data-table td { padding: 0.5rem; border-bottom: 1px solid var(--color-border); text-align: left; }
  `]
})
export class ReportsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private errors = inject(ApiErrorService);

  readonly reports: ReportDef[] = [
    { id: 'behandlingar', title: 'Behandlingar', hasRange: true },
    { id: 'intakter', title: 'Intäkter', hasRange: true },
    { id: 'uppfoljningar', title: 'Uppföljningar', hasRange: false },
    { id: 'osignerade', title: 'Osignerade', hasRange: false },
    { id: 'uteblivna', title: 'Uteblivna', hasRange: true },
    { id: 'klientaktivitet', title: 'Vilande kunder', hasRange: false, hasMonths: true },
    { id: 'bokningskallor', title: 'Bokningskällor', hasRange: true }
  ];

  active = signal<ReportId>('behandlingar');
  from = '';
  to = '';
  months = 12;
  loading = signal(false);
  loaded = signal(false);
  error = signal('');
  columns = signal<string[]>([]);
  rows = signal<string[][]>([]);

  ngOnInit(): void {
    const today = new Date();
    const threeMonthsAgo = new Date(today);
    threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
    this.to = this.fmt(today);
    this.from = this.fmt(threeMonthsAgo);
    this.load();
  }

  current(): ReportDef | undefined {
    return this.reports.find(r => r.id === this.active());
  }

  select(id: ReportId): void {
    this.active.set(id);
    this.loaded.set(false);
    this.rows.set([]);
    this.load();
  }

  load(): void {
    const id = this.active();
    this.loading.set(true);
    this.error.set('');
    const params: Record<string, string | number> = {};
    if (this.current()?.hasRange) {
      params['from'] = this.from;
      params['to'] = this.to;
    }
    if (this.current()?.hasMonths) params['months'] = this.months;

    this.api.get<unknown>(`/api/app/reports/${id}`, params).subscribe({
      next: data => {
        this.applyData(id, data);
        this.loading.set(false);
        this.loaded.set(true);
      },
      error: err => {
        this.error.set(this.errors.message(err));
        this.loading.set(false);
      }
    });
  }

  exportCsv(): void {
    const id = this.active();
    const params: Record<string, string | number> = { format: 'csv' };
    if (this.current()?.hasRange) {
      params['from'] = this.from;
      params['to'] = this.to;
    }
    if (this.current()?.hasMonths) params['months'] = this.months;
    this.api.downloadGet(`/api/app/reports/${id}`, params).subscribe({
      next: blob => this.saveBlob(blob, `${id}.csv`),
      error: err => this.toast.error(this.errors.message(err))
    });
  }

  private saveBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
    this.toast.success('CSV exporterad.');
  }

  private applyData(id: ReportId, data: unknown): void {
    const arr = Array.isArray(data) ? data : (data as { clients?: unknown[] })?.clients ?? [];
    switch (id) {
      case 'behandlingar':
        this.columns.set(['Månad', 'Behandling', 'Antal', 'Intäkt']);
        this.rows.set((arr as { month: string; treatmentName: string; count: number; revenueEstimate: number }[]).map(r =>
          [r.month?.slice?.(0, 10) ?? r.month, r.treatmentName, String(r.count), String(r.revenueEstimate)]));
        break;
      case 'intakter':
        this.columns.set(['Månad', 'Exkl moms', 'Inkl moms', 'Antal']);
        this.rows.set((arr as { month: string; exclVat: number; inclVat: number; bookingCount: number }[]).map(r =>
          [r.month?.slice?.(0, 7) ?? r.month, String(r.exclVat), String(r.inclVat), String(r.bookingCount)]));
        break;
      case 'uppfoljningar':
        this.columns.set(['Häst', 'Kund', 'Behandling', 'Dagar sen']);
        this.rows.set((arr as { horseName: string; ownerName: string; lastTreatment: string; daysOverdue: number }[]).map(r =>
          [r.horseName, r.ownerName, r.lastTreatment, String(r.daysOverdue)]));
        break;
      case 'osignerade':
        this.columns.set(['Häst', 'Kund', 'Behandling', 'Timmar']);
        this.rows.set((arr as { horseName: string; ownerName: string; treatment: string; hoursOld: number }[]).map(r =>
          [r.horseName, r.ownerName, r.treatment, String(r.hoursOld)]));
        break;
      case 'uteblivna':
        this.columns.set(['Kund', 'Uteblivna', 'Sen avbokning', 'Totalt']);
        this.rows.set((arr as { ownerName: string; noShows: number; lateCancellations: number; total: number }[]).map(r =>
          [r.ownerName, String(r.noShows), String(r.lateCancellations), String(r.total)]));
        break;
      case 'klientaktivitet':
        this.columns.set(['Kund', 'E-post', 'Telefon', 'Senaste bokning']);
        this.rows.set((arr as { name: string; email: string; phone: string; lastBookingAt?: string }[]).map(r =>
          [r.name, r.email, r.phone, r.lastBookingAt ? r.lastBookingAt.slice(0, 10) : '—']));
        break;
      case 'bokningskallor':
        this.columns.set(['Månad', 'Källa', 'Antal']);
        this.rows.set((arr as { month: string; source: string; count: number }[]).map(r =>
          [r.month?.slice?.(0, 7) ?? r.month, r.source, String(r.count)]));
        break;
    }
  }

  private fmt(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
