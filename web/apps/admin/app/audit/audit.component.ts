import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent } from '@equijournal/ui';
import { Api } from '../api';

interface AuditRow {
  id: string;
  actorId?: string;
  action: string;
  entityType: string;
  entityId: string;
  timestamp: string;
}

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent, DatePipe],
  template: `
    <div class="page">
      <ej-page-header title="Granskningslogg" subtitle="Vem gjorde vad och när. Loggen kan inte ändras." />

      <div class="card filters">
        <input class="input" [(ngModel)]="entityType" placeholder="Entitetstyp" />
        <input class="input" [(ngModel)]="action" placeholder="Åtgärd" />
        <input class="input" type="date" [(ngModel)]="from" aria-label="Från datum" />
        <input class="input" type="date" [(ngModel)]="to" aria-label="Till datum" />
        <button type="button" class="btn-primary" (click)="load()">Filtrera</button>
        <button type="button" class="btn-secondary" (click)="exportCsv()">Exportera CSV</button>
      </div>

      <div class="card table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Tidpunkt</th>
              <th scope="col">Användare</th>
              <th scope="col">Åtgärd</th>
              <th scope="col">Entitet</th>
            </tr>
          </thead>
          <tbody>
            @for (row of items(); track row.id) {
              <tr>
                <td>{{ row.timestamp | date:'yyyy-MM-dd HH:mm' }}</td>
                <td>{{ row.actorId || '—' }}</td>
                <td>{{ row.action }}</td>
                <td>{{ row.entityType }} · {{ row.entityId }}</td>
              </tr>
            } @empty {
              <tr><td colspan="4">Inga poster.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .filters { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
    .filters .input { flex: 1; min-width: 8rem; min-height: var(--tap-min); }
    .table-wrap { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: var(--text-sm); }
    .data-table th, .data-table td { padding: 0.5rem; border-bottom: 1px solid var(--color-border); text-align: left; }
  `]
})
export class AuditComponent implements OnInit {
  private api = inject(Api);
  items = signal<AuditRow[]>([]);
  entityType = '';
  action = '';
  from = '';
  to = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    const params: Record<string, string> = {};
    if (this.entityType) params['entityType'] = this.entityType;
    if (this.action) params['action'] = this.action;
    if (this.from) params['from'] = new Date(this.from).toISOString();
    if (this.to) params['to'] = new Date(this.to + 'T23:59:59').toISOString();
    this.api.get<{ items: AuditRow[] }>('/api/app/audit', params).subscribe(res => this.items.set(res.items ?? []));
  }

  exportCsv(): void {
    const params: Record<string, string> = {};
    if (this.from) params['from'] = new Date(this.from).toISOString();
    if (this.to) params['to'] = new Date(this.to + 'T23:59:59').toISOString();
    this.api.downloadGet('/api/app/audit/export', params).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'audit-log.csv';
        a.click();
        URL.revokeObjectURL(url);
      }
    });
  }
}
