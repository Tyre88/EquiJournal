import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent } from '@equijournal/ui';
import { Api } from '../api';
import { statusClass, statusText } from '../bookings/status';

interface HorseRow {
  id: string;
  name: string;
  species: string;
  sex: string;
  birthYear?: number;
  status: string;
  ownerName: string;
}

@Component({
  selector: 'app-horses',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Hästar">
        <a class="btn-primary" routerLink="/horses/create"><ej-icon name="plus" [size]="16" /> Ny häst</a>
      </ej-page-header>
      <div class="toolbar">
        <label class="sr-only" for="horseSearch">Sök hästar</label>
        <input id="horseSearch" class="search-input" placeholder="Sök häst eller ägare" (input)="onSearch($event)" />
      </div>
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (horses().length === 0) {
        <ej-empty-state icon="horse" title="Inga hästar" description="Registrera en häst för att kunna skriva journal.">
          <a class="btn-primary" routerLink="/horses/create">Ny häst</a>
        </ej-empty-state>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>Namn</th><th>Ägare</th><th>Kön</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (h of horses(); track h.id) {
                <tr (click)="router.navigate(['/horses', h.id])">
                  <td data-label="Namn"><strong>{{ h.name }}</strong></td>
                  <td data-label="Ägare">{{ h.ownerName }}</td>
                  <td data-label="Kön">{{ h.sex }}</td>
                  <td data-label="Status"><span [class]="statusClass(h.status)">{{ statusText(h.status) }}</span></td>
                  <td data-label="Åtgärder">
                    <a class="btn-ghost btn-sm" [routerLink]="['/horses', h.id, 'edit']" (click)="$event.stopPropagation()">Redigera</a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`.sr-only { position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);} tr{cursor:pointer;}`]
})
export class HorsesComponent implements OnInit {
  private api = inject(Api);
  router = inject(Router);
  horses = signal<HorseRow[]>([]);
  loading = signal(false);
  error = signal('');
  statusClass = statusClass;
  statusText = statusText;
  private search$ = new Subject<string>();

  ngOnInit(): void {
    this.load();
    this.search$.pipe(debounceTime(250), distinctUntilChanged()).subscribe(q => {
      if (!q) { this.load(); return; }
      this.loading.set(true);
      this.api.post<HorseRow[]>('/api/app/horses/search', { query: q }).subscribe({
        next: rows => { this.horses.set(rows); this.loading.set(false); },
        error: () => { this.error.set('Sökningen misslyckades.'); this.loading.set(false); }
      });
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.get<HorseRow[]>('/api/app/horses').subscribe({
      next: rows => { this.horses.set(rows); this.loading.set(false); },
      error: () => { this.error.set('Kunde inte ladda hästar.'); this.loading.set(false); }
    });
  }

  onSearch(e: Event): void {
    this.search$.next((e.target as HTMLInputElement).value);
  }
}
