import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent } from '@equijournal/ui';
import { environment } from '../../environments/environment';
import { statusClass, statusText } from '../bookings/status';

const API_URL = environment.apiUrl;

interface JournalEntry {
  id: string;
  horseName: string;
  ownerName: string;
  treatmentTypeName: string;
  performedAt: string;
  status: string;
}

type StatusFilter = 'All' | 'Draft' | 'Signed';

@Component({
  selector: 'app-journals',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Journaler">
        <a routerLink="/journals/new" class="btn-primary"><ej-icon name="plus" [size]="16" /> Ny journal</a>
      </ej-page-header>

      <div class="toolbar">
        <input class="search-input" placeholder="Sök i journaler…" (input)="onSearch($event)" />
        <div class="seg">
          <button type="button" [class.active]="filter() === 'All'" (click)="filter.set('All')">Alla</button>
          <button type="button" [class.active]="filter() === 'Draft'" (click)="filter.set('Draft')">Utkast</button>
          <button type="button" [class.active]="filter() === 'Signed'" (click)="filter.set('Signed')">Signerade</button>
        </div>
      </div>

      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (filteredJournals().length === 0) {
        <ej-empty-state icon="file-text" title="Inga journaler" description="Skriv en journal i anslutning till behandlingen.">
          <a routerLink="/journals/new" class="btn-primary">Ny journal</a>
        </ej-empty-state>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr><th>Datum</th><th>Häst</th><th>Ägare</th><th>Status</th><th></th></tr>
            </thead>
            <tbody>
              @for (journal of filteredJournals(); track journal.id) {
                <tr>
                  <td data-label="Datum">{{ formatDate(journal.performedAt) }}</td>
                  <td data-label="Häst">{{ journal.horseName }}</td>
                  <td data-label="Ägare">{{ journal.ownerName }}</td>
                  <td data-label="Status"><span [class]="statusClass(journal.status)">{{ statusText(journal.status) }}</span></td>
                  <td data-label="Åtgärder"><a [routerLink]="['/journals', journal.id]" class="btn-secondary btn-sm">Visa</a></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `
})
export class JournalsComponent implements OnInit {
  private http = inject(HttpClient);
  journals = signal<JournalEntry[]>([]);
  loading = signal(true);
  error = signal('');
  filter = signal<StatusFilter>('All');
  statusClass = statusClass;
  statusText = statusText;

  filteredJournals = computed(() => {
    const f = this.filter();
    if (f === 'All') return this.journals();
    return this.journals().filter(j => j.status === f);
  });

  ngOnInit(): void {
    this.loadJournals();
  }

  private mapRow(j: any): JournalEntry {
    return {
      id: j.id,
      horseName: j.horseName || j.horse?.name || 'Okänd häst',
      ownerName: j.ownerName || j.horse?.ownerName || 'Okänd',
      treatmentTypeName: j.treatmentTypeName || j.treatmentType?.name || '—',
      performedAt: j.performedAt || j.performedAtLocal || '',
      status: j.status || 'Draft'
    };
  }

  private loadJournals(): void {
    this.http.get<any>(`${API_URL}/api/app/journals`).subscribe({
      next: (response: any) => {
        const list = response.journals ?? response.Journals ?? response.results ?? response;
        this.journals.set((Array.isArray(list) ? list : []).map((j: any) => this.mapRow(j)));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda journaler.');
        this.loading.set(false);
      }
    });
  }

  onSearch(event: Event): void {
    const q = (event.target as HTMLInputElement).value.trim();
    if (q.length < 2) { this.loadJournals(); return; }
    this.http.post<any>(`${API_URL}/api/app/journals/search`, { query: q }).subscribe({
      next: (list) => this.journals.set((Array.isArray(list) ? list : []).map((j: any) => this.mapRow(j))),
      error: () => this.error.set('Sökningen misslyckades.')
    });
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '—';
    try {
      return new Date(dateStr).toLocaleDateString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit' });
    } catch {
      return dateStr;
    }
  }
}
