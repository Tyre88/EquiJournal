import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmService, EjEmptyStateComponent, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { EntityHistoryComponent } from '../../audit/entity-history.component';
import { Api } from '../../api';
import { statusClass, statusText } from '../../bookings/status';

interface HorseDetail {
  id: string;
  ownerId: string;
  ownerName: string;
  name: string;
  species: string;
  breed?: string;
  sex: string;
  birthYear?: number;
  ageGroup?: string;
  identity?: string;
  colour?: string;
  markings?: string;
  stableLocation?: string;
  status: string;
}

interface JournalRow {
  id: string;
  performedAt: string;
  status: string;
  anamnes: string;
}

@Component({
  selector: 'app-horse-detail',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EntityHistoryComponent],
  template: `
    <div class="page">
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @if (horse(); as h) {
        <ej-page-header [title]="h.name" [subtitle]="h.species + ' · ' + h.sex + ' · ' + (h.birthYear || h.ageGroup || '—')" backHref="/horses">
          <a class="btn-secondary" [routerLink]="['/horses', h.id, 'edit']">Redigera</a>
          <button type="button" class="btn-secondary" (click)="exportPdf()">PDF</button>
          <button type="button" class="btn-danger" (click)="archive()">Arkivera</button>
          <a class="btn-primary" [routerLink]="['/journals/new']" [queryParams]="{ horseId: h.id }">Ny journal</a>
        </ej-page-header>
        <div class="card">
          <dl>
            <div class="detail-row"><dt>Ägare</dt><dd><a [routerLink]="['/owners', h.ownerId]">{{ h.ownerName }}</a></dd></div>
            <div class="detail-row"><dt>Status</dt><dd><span [class]="statusClass(h.status)">{{ statusText(h.status) }}</span></dd></div>
            @if (h.identity) { <div class="detail-row"><dt>Identitet</dt><dd>{{ h.identity }}</dd></div> }
            @if (h.stableLocation) { <div class="detail-row"><dt>Stall</dt><dd>{{ h.stableLocation }}</dd></div> }
          </dl>
        </div>
        <app-entity-history entityType="Horse" [entityId]="h.id" />
        <h2 class="card-title" style="margin-top:1.25rem">Journalhistorik</h2>
        @if (journals().length === 0) {
          <ej-empty-state icon="file-text" title="Inga journaler" description="Skriv första journalen för den här hästen.">
            <a class="btn-primary" [routerLink]="['/journals/new']" [queryParams]="{ horseId: h.id }">Ny journal</a>
          </ej-empty-state>
        } @else {
          <div class="card" style="padding:0">
            <ul class="jlist">
              @for (j of journals(); track j.id) {
                <li>
                  <a [routerLink]="['/journals', j.id]">
                    {{ format(j.performedAt) }}
                    <span [class]="statusClass(j.status)">{{ statusText(j.status) }}</span>
                  </a>
                </li>
              }
            </ul>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .jlist { list-style: none; margin: 0; padding: 0; }
    .jlist a { display: flex; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; text-decoration: none; color: inherit; border-bottom: 1px solid var(--color-border); }
    .jlist li:last-child a { border-bottom: 0; }
  `]
})
export class HorseDetailComponent implements OnInit {
  private api = inject(Api);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private confirm = inject(ConfirmService);
  private toast = inject(ToastService);
  horse = signal<HorseDetail | null>(null);
  journals = signal<JournalRow[]>([]);
  loading = signal(true);
  error = signal('');
  statusClass = statusClass;
  statusText = statusText;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.api.get<HorseDetail>(`/api/app/horses/${id}`).subscribe({
      next: h => { this.horse.set(h); this.loading.set(false); },
      error: () => { this.error.set('Kunde inte ladda hästen.'); this.loading.set(false); }
    });
    this.api.get<{ journals: JournalRow[] }>(`/api/app/journals/by-horse/${id}`, { page: 1, pageSize: 50 }).subscribe({
      next: res => this.journals.set(res.journals ?? (res as any).Journals ?? []),
      error: () => this.journals.set([])
    });
  }

  async archive(): Promise<void> {
    const h = this.horse();
    if (!h) return;
    const ok = await this.confirm.confirm({
      title: 'Arkivera hästen',
      message: 'Hästen döljs från listor. Journaler behålls.',
      confirmLabel: 'Arkivera',
      destructive: true
    });
    if (!ok) return;
    this.api.delete(`/api/app/horses/${h.id}`).subscribe({
      next: () => { this.toast.success('Hästen arkiverades.'); this.router.navigate(['/horses']); },
      error: () => this.toast.error('Kunde inte arkivera hästen.')
    });
  }

  exportPdf(): void {
    const h = this.horse();
    if (!h) return;
    this.api.download(`/api/app/journals/export/pdf-horse/${h.id}`).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `journal_${h.name}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Kunde inte skapa PDF.')
    });
  }

  format(d: string): string {
    return d ? new Date(d).toLocaleString('sv-SE') : '—';
  }
}
