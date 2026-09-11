import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent } from '@equijournal/ui';
import { environment } from '../../environments/environment';

const API_URL = environment.apiUrl;

interface Owner {
  id: string;
  name: string;
  email: string;
  phone: string;
  addressStreet: string;
  addressPostcode: string;
  addressCity: string;
  notes: string;
  marketingConsent: boolean;
}

@Component({
  selector: 'app-owners',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Kunder">
        <a class="btn-primary" routerLink="/owners/create"><ej-icon name="plus" [size]="16" /> Ny kund</a>
      </ej-page-header>

      <div class="toolbar">
        <label class="sr-only" for="ownerSearch">Sök kunder</label>
        <input id="ownerSearch" class="search-input" placeholder="Sök namn, e-post eller telefon" (input)="onSearchInput($event)" />
      </div>

      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (owners().length === 0) {
        <ej-empty-state icon="users" title="Inga kunder" description="Lägg till en kund för att kunna boka och journalföra.">
          <a class="btn-primary" routerLink="/owners/create">Ny kund</a>
        </ej-empty-state>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr><th>Namn</th><th>E-post</th><th>Telefon</th><th>Ort</th><th></th></tr>
            </thead>
            <tbody>
              @for (owner of owners(); track owner.id) {
                <tr (click)="navigateToDetail(owner.id)">
                  <td data-label="Namn"><strong>{{ owner.name }}</strong></td>
                  <td data-label="E-post">{{ owner.email }}</td>
                  <td data-label="Telefon">{{ owner.phone || '—' }}</td>
                  <td data-label="Ort">{{ owner.addressCity || '—' }}</td>
                  <td data-label="Åtgärder">
                    <a class="btn-ghost btn-sm" [routerLink]="['/owners', owner.id, 'edit']" (click)="$event.stopPropagation()">Redigera</a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0,0,0,0); }
    tr { cursor: pointer; }
  `]
})
export class OwnersComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private searchSubject = new Subject<string>();

  owners = signal<Owner[]>([]);
  loading = signal(false);
  error = signal('');

  ngOnInit(): void {
    this.fetchOwners();
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(query => {
      this.searchOwners(query);
    });
  }

  fetchOwners(): void {
    this.loading.set(true);
    this.http.get<Owner[]>(`${API_URL}/api/app/owners`).subscribe({
      next: (owners) => { this.owners.set(owners); this.loading.set(false); },
      error: () => { this.error.set('Kunde inte ladda kunder.'); this.loading.set(false); }
    });
  }

  searchOwners(query: string): void {
    if (!query.trim()) { this.fetchOwners(); return; }
    this.loading.set(true);
    this.http.post<Owner[]>(`${API_URL}/api/app/owners/search`, { query }).subscribe({
      next: (owners) => { this.owners.set(owners); this.loading.set(false); },
      error: () => { this.error.set('Sökningen misslyckades.'); this.loading.set(false); }
    });
  }

  onSearchInput(event: Event): void {
    this.searchSubject.next((event.target as HTMLInputElement).value);
  }

  navigateToDetail(id: string): void { this.router.navigate(['/owners', id]); }
}
