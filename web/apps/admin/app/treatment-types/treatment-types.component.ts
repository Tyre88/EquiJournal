import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ConfirmService, EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { environment } from '../../environments/environment';

interface TreatmentType {
  id: string;
  name: string;
  shortDescription: string;
  durationMinutes: number;
  priceExclVat: number;
  vatRate: number;
  status: string;
  isActive?: boolean;
  colour?: string;
}

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-treatment-types',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Behandlingstyper" subtitle="Hantera dina behandlingstyper.">
        <a routerLink="/treatment-types/create" class="btn-primary"><ej-icon name="plus" [size]="16" /> Ny behandlingstyp</a>
      </ej-page-header>

      <div class="seg" style="margin-bottom:1rem">
        <button type="button" [class.active]="statusFilter() === 'all'" (click)="setStatusFilter('all')">Alla</button>
        <button type="button" [class.active]="statusFilter() === 'active'" (click)="setStatusFilter('active')">Aktiva</button>
        <button type="button" [class.active]="statusFilter() === 'inactive'" (click)="setStatusFilter('inactive')">Inaktiva</button>
      </div>

      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading">Laddar…</p> }
      @else if (filteredTypes().length === 0) {
        <ej-empty-state icon="settings" title="Inga behandlingstyper" description="Skapa en typ för att kunna boka och journalföra.">
          <a routerLink="/treatment-types/create" class="btn-primary">Ny behandlingstyp</a>
        </ej-empty-state>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr><th>Namn</th><th>Tid</th><th>Pris (exkl. moms)</th><th>Status</th><th></th></tr>
            </thead>
            <tbody>
              @for (type of filteredTypes(); track type.id) {
                <tr>
                  <td data-label="Namn">
                    <span class="dot" [style.background]="type.colour || 'var(--color-primary)'"></span>
                    {{ type.name }}
                  </td>
                  <td data-label="Tid">{{ type.durationMinutes }} min</td>
                  <td data-label="Pris">{{ (type.priceExclVat ?? 0).toLocaleString('sv-SE') }} kr</td>
                  <td data-label="Status">
                    <span [class]="isActive(type) ? 'badge badge-active' : 'badge badge-inactive'">
                      {{ isActive(type) ? 'Aktiv' : 'Inaktiv' }}
                    </span>
                  </td>
                  <td data-label="Åtgärder">
                    <a [routerLink]="['/treatment-types', type.id, 'edit']" class="btn-ghost btn-sm">Redigera</a>
                    @if (isActive(type)) {
                      <button type="button" class="btn-danger btn-sm" (click)="toggleStatus(type)">Avaktivera</button>
                    } @else {
                      <button type="button" class="btn-secondary btn-sm" (click)="toggleStatus(type)">Aktivera</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`.dot { display:inline-block; width:10px; height:10px; border-radius:50%; margin-right:0.45rem; }`]
})
export class TreatmentTypesComponent implements OnInit {
  private http = inject(HttpClient);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);

  treatmentTypes = signal<TreatmentType[]>([]);
  statusFilter = signal<StatusFilter>('all');
  loading = signal(false);
  error = signal('');
  filteredTypes = signal<TreatmentType[]>([]);

  ngOnInit(): void {
    this.loadTreatmentTypes();
  }

  loadTreatmentTypes(): void {
    this.loading.set(true);
    this.http.get<TreatmentType[]>(`${environment.apiUrl}/api/app/treatment-types`).subscribe({
      next: (types) => {
        this.treatmentTypes.set(types);
        this.applyFilter();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda behandlingstyper.');
        this.loading.set(false);
      }
    });
  }

  setStatusFilter(filter: StatusFilter): void {
    this.statusFilter.set(filter);
    this.applyFilter();
  }

  applyFilter(): void {
    const filter = this.statusFilter();
    const types = this.treatmentTypes();
    if (filter === 'all') this.filteredTypes.set(types);
    else if (filter === 'active') this.filteredTypes.set(types.filter(t => this.isActive(t)));
    else this.filteredTypes.set(types.filter(t => !this.isActive(t)));
  }

  isActive(type: TreatmentType): boolean {
    return type.status === 'Active' || type.isActive === true;
  }

  async toggleStatus(type: TreatmentType): Promise<void> {
    if (this.isActive(type)) {
      const ok = await this.confirm.confirm({
        title: 'Avaktivera behandlingstyp',
        message: `${type.name} döljs från nya bokningar.`,
        confirmLabel: 'Avaktivera',
        destructive: true
      });
      if (!ok) return;
    }
    const path = this.isActive(type)
      ? `${environment.apiUrl}/api/app/treatment-types/${type.id}/deactivate`
      : `${environment.apiUrl}/api/app/treatment-types/${type.id}/activate`;
    this.http.post(path, {}).subscribe({
      next: () => {
        this.toast.success(this.isActive(type) ? 'Avaktiverad.' : 'Aktiverad.');
        this.loadTreatmentTypes();
      },
      error: () => this.toast.error('Kunde inte ändra status.')
    });
  }
}
