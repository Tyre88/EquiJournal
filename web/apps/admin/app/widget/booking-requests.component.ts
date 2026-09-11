import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfirmService, EjEmptyStateComponent, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { BookingListItem } from '../bookings/booking.models';
import { statusClass, statusText } from '../bookings/status';

@Component({
  selector: 'app-booking-requests',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent],
  template: `
    <div class="page">
      <ej-page-header title="Inkomna förfrågningar" subtitle="Bokningar från widgeten som väntar på godkännande." />
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (rows().length === 0) {
        <ej-empty-state icon="file-text" title="Inga förfrågningar" description="När en kund har bekräftat sin e-post visas bokningen här." />
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead>
              <tr><th>Tid</th><th>Kund</th><th>Häst</th><th>Behandling</th><th></th></tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.id) {
                <tr>
                  <td data-label="Tid">{{ format(row.startsAt) }}</td>
                  <td data-label="Kund">{{ row.ownerName }}</td>
                  <td data-label="Häst">{{ row.horseName }}</td>
                  <td data-label="Behandling">
                    {{ row.treatmentName }}
                    <span class="badge" [class]="statusClass(row.status)">{{ statusText(row.status) }}</span>
                  </td>
                  <td data-label="Åtgärder">
                    <button type="button" class="btn-primary btn-sm" (click)="approve(row)">Godkänn</button>
                    <button type="button" class="btn-danger btn-sm" (click)="decline(row)">Avslå</button>
                    <a class="btn-ghost btn-sm" [routerLink]="['/bookings', row.id]">Öppna</a>
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
export class BookingRequestsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);
  rows = signal<BookingListItem[]>([]);
  error = signal('');

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.get<BookingListItem[]>('/api/app/bookings', { status: 'Requested', source: 'Widget', emailVerified: true }).subscribe({
      next: (rows) => this.rows.set(rows),
      error: () => this.error.set('Kunde inte hämta förfrågningar.')
    });
  }

  approve(row: BookingListItem): void {
    this.api.post(`/api/app/bookings/${row.id}/approve`, {}).subscribe({
      next: () => { this.toast.success('Bokningen godkändes.'); this.reload(); },
      error: () => this.error.set('Kunde inte godkänna.')
    });
  }

  async decline(row: BookingListItem): Promise<void> {
    const ok = await this.confirm.confirm({
      title: 'Avslå förfrågan?',
      message: 'Tiden släpps och kunden behöver boka igen.',
      confirmLabel: 'Avslå',
      destructive: true
    });
    if (!ok) return;
    this.api.post(`/api/app/bookings/${row.id}/cancel`, { reason: 'Avslagen' }).subscribe({
      next: () => { this.toast.success('Förfrågan avslogs.'); this.reload(); },
      error: () => this.error.set('Kunde inte avslå.')
    });
  }

  format(value: string): string {
    return new Date(value).toLocaleString('sv-SE', { dateStyle: 'short', timeStyle: 'short' });
  }

  statusClass = statusClass;
  statusText = statusText;
}
