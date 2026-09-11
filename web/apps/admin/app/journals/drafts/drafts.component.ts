import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { ConfirmService, EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { environment } from '../../../environments/environment';

const API_URL = environment.apiUrl;

interface JournalEntry {
  id: string;
  horseName: string;
  ownerName: string;
  treatmentTypeName: string;
  performedAt: string;
  status: string;
  createdAt: string;
}

@Component({
  selector: 'app-drafts',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Utkast" subtitle="Osignerade journaler som väntar på att färdigställas.">
        <a routerLink="/journals/new" class="btn-primary"><ej-icon name="plus" [size]="16" /> Ny journal</a>
      </ej-page-header>

      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (drafts().length === 0) {
        <ej-empty-state icon="pen" title="Inga utkast" description="När en journal sparas utan signering hamnar den här.">
          <a routerLink="/journals/new" class="btn-primary">Ny journal</a>
        </ej-empty-state>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr><th>Datum</th><th>Häst</th><th>Ägare</th><th>Behandling</th><th>Skapad</th><th></th></tr>
            </thead>
            <tbody>
              @for (journal of drafts(); track journal.id) {
                <tr>
                  <td data-label="Datum">{{ formatDate(journal.performedAt) }}</td>
                  <td data-label="Häst">{{ journal.horseName }}</td>
                  <td data-label="Ägare">{{ journal.ownerName }}</td>
                  <td data-label="Behandling">{{ journal.treatmentTypeName }}</td>
                  <td data-label="Skapad">{{ formatDate(journal.createdAt) }}</td>
                  <td data-label="Åtgärder">
                    <a class="btn-secondary btn-sm" [routerLink]="['/journals', journal.id, 'edit']">Redigera</a>
                    <button type="button" class="btn-danger btn-sm" (click)="deleteJournal(journal.id)">Radera</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `
})
export class DraftsComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private confirm = inject(ConfirmService);
  private toast = inject(ToastService);

  drafts = signal<JournalEntry[]>([]);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.loadDrafts();
  }

  private loadDrafts(): void {
    this.http.get<any>(`${API_URL}/api/app/journals/drafts`).subscribe({
      next: (response: any) => {
        const list = response.drafts ?? response.Drafts ?? response.results ?? response;
        this.drafts.set((Array.isArray(list) ? list : []).map((j: any) => ({
          id: j.id,
          horseName: j.horseName || j.horse?.name || 'Okänd häst',
          ownerName: j.ownerName || j.horse?.ownerName || 'Okänd',
          treatmentTypeName: j.treatmentTypeName || j.treatmentType?.name || '—',
          performedAt: j.performedAt || j.performedAtLocal || '',
          createdAt: j.createdAt || '',
          status: j.status || 'Draft'
        })));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda utkast.');
        this.loading.set(false);
      }
    });
  }

  async deleteJournal(id: string): Promise<void> {
    const ok = await this.confirm.confirm({
      title: 'Radera utkast',
      message: 'Utkastet tas bort. Detta kan inte ångras.',
      confirmLabel: 'Radera',
      destructive: true
    });
    if (!ok) return;
    this.http.delete(`${API_URL}/api/app/journals/${id}`).subscribe({
      next: () => {
        this.drafts.update(drafts => drafts.filter(d => d.id !== id));
        this.toast.success('Utkastet raderades.');
      },
      error: () => this.toast.error('Misslyckades med att radera journalen.')
    });
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '—';
    try {
      return new Date(dateStr).toLocaleDateString('sv-SE', {
        year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
      });
    } catch {
      return dateStr;
    }
  }
}
