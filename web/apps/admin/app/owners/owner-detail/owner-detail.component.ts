import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ConfirmService, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { environment } from '../../../environments/environment';
import { AddHorseComponent } from './add-horse/add-horse.component';

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

interface Horse {
  id: string;
  name: string;
  species: string;
  breed: string;
  sex: string;
}

@Component({
  selector: 'app-owner-detail',
  standalone: true,
  imports: [AddHorseComponent, RouterLink, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header [title]="owner()?.name || 'Kund'" backHref="/owners">
        <a class="btn-secondary" [routerLink]="['/owners', ownerId(), 'edit']">Redigera</a>
        <button class="btn-danger" type="button" (click)="archive()">Arkivera</button>
      </ej-page-header>

      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @else if (owner(); as o) {
        <div class="grid">
          <div class="card">
            <h2 class="card-title">Kundinformation</h2>
            <dl>
              <div class="detail-row"><dt>Namn</dt><dd>{{ o.name }}</dd></div>
              <div class="detail-row"><dt>E-post</dt><dd><a [href]="'mailto:' + o.email">{{ o.email }}</a></dd></div>
              @if (o.phone) {
                <div class="detail-row"><dt>Telefon</dt><dd><a [href]="'tel:' + o.phone">{{ o.phone }}</a></dd></div>
              }
              @if (addressFull()) {
                <div class="detail-row"><dt>Adress</dt><dd>{{ addressFull() }}</dd></div>
              }
              @if (o.notes) {
                <div class="detail-row"><dt>Anteckningar</dt><dd>{{ o.notes }}</dd></div>
              }
              <div class="detail-row"><dt>Marknadsföring</dt><dd>{{ o.marketingConsent ? 'Ja' : 'Nej' }}</dd></div>
            </dl>
          </div>
          <div class="card">
            <div class="card-head">
              <h2 class="card-title">Hästar ({{ horses().length }})</h2>
              <button class="btn-primary" type="button" (click)="showAddHorse = true">Ny häst</button>
            </div>
            @if (showAddHorse) {
              <app-add-horse [ownerId]="ownerId() ?? ''" (horseAdded)="onHorseAdded()" (cancelled)="showAddHorse = false" />
            }
            @if (horses().length === 0) {
              <p class="muted">Inga hästar registrerade för denna kund.</p>
            } @else {
              <div class="horses">
                @for (horse of horses(); track horse.id) {
                  <a class="horse" [routerLink]="['/horses', horse.id]">
                    <strong>{{ horse.name }}</strong>
                    <span class="muted">{{ horse.species }}{{ horse.breed ? ' / ' + horse.breed : '' }}{{ horse.sex ? ' / ' + horse.sex : '' }}</span>
                  </a>
                }
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; gap: 1rem; }
    .card-head { display: flex; justify-content: space-between; gap: 0.75rem; align-items: center; }
    .horses { display: grid; gap: 0.5rem; }
    .horse { display: flex; flex-direction: column; padding: 0.75rem; border-radius: 8px; background: var(--color-surface-muted); text-decoration: none; color: inherit; }
    @media (min-width: 768px) { .grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class OwnerDetailComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private confirm = inject(ConfirmService);
  private toast = inject(ToastService);

  ownerId = signal<string | null>(null);
  owner = signal<Owner | null>(null);
  horses = signal<Horse[]>([]);
  loading = signal(true);
  error = signal('');
  showAddHorse = false;
  addressFull = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.ownerId.set(id);
      this.fetchOwner(id);
      this.fetchHorses(id);
    }
  }

  fetchOwner(id: string): void {
    this.loading.set(true);
    this.http.get<Owner>(`${API_URL}/api/app/owners/${id}`).subscribe({
      next: (owner) => {
        this.owner.set(owner);
        this.addressFull.set([owner.addressStreet, owner.addressPostcode, owner.addressCity].filter(Boolean).join(', '));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda kundinformation.');
        this.loading.set(false);
      }
    });
  }

  fetchHorses(ownerId: string): void {
    this.http.get<Horse[]>(`${API_URL}/api/app/horses/by-owner/${ownerId}`).subscribe({
      next: horses => this.horses.set(horses),
      error: () => this.horses.set([])
    });
  }

  onHorseAdded(): void {
    this.showAddHorse = false;
    const id = this.ownerId();
    if (id) this.fetchHorses(id);
  }

  async archive(): Promise<void> {
    const id = this.ownerId();
    if (!id) return;
    const ok = await this.confirm.confirm({
      title: 'Arkivera kunden',
      message: 'Kunden döljs från listor men journaler behålls.',
      confirmLabel: 'Arkivera',
      destructive: true
    });
    if (!ok) return;
    this.http.delete(`${API_URL}/api/app/owners/${id}`).subscribe({
      next: () => {
        this.toast.success('Kunden arkiverades.');
        this.router.navigate(['/owners']);
      },
      error: (err) => this.error.set(err.error ?? 'Kunde inte arkivera kunden.')
    });
  }
}
