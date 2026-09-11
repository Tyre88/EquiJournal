import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmService, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { EntityHistoryComponent } from '../audit/entity-history.component';
import { Api } from '../api';
import { BookingListItem } from './booking.models';
import { statusClass, statusText } from './status';

@Component({
  selector: 'app-booking-detail',
  standalone: true,
  imports: [RouterLink, FormsModule, EjPageHeaderComponent, DatePipe, EntityHistoryComponent],
  template: `
    <div class="page page--narrow">
      @if (row(); as b) {
        <ej-page-header [title]="b.treatmentName" [subtitle]="format(b.startsAt) + '–' + time(b.endsAt)" backHref="/schema">
          <span class="badge" [class]="statusClass(b.status)">{{ statusText(b.status) }}</span>
        </ej-page-header>

        <div class="card">
          <dl>
            <div class="detail-row">
              <dt>Häst</dt>
              <dd><a [routerLink]="['/horses', b.horseId]">{{ b.horseName }}</a></dd>
            </div>
            <div class="detail-row">
              <dt>Kund</dt>
              <dd>{{ b.ownerName }}</dd>
            </div>
            @if (b.ownerPhone) {
              <div class="detail-row">
                <dt>Telefon</dt>
                <dd><a [href]="'tel:' + b.ownerPhone">{{ b.ownerPhone }}</a></dd>
              </div>
            }
            <div class="detail-row">
              <dt>Adress</dt>
              <dd>{{ address(b) || '—' }}</dd>
            </div>
          </dl>
        </div>

        <div class="card">
          <div class="field">
            <label class="field-label" for="clientNote">Kundanteckning</label>
            <textarea id="clientNote" class="input" [(ngModel)]="clientNote" rows="3"></textarea>
          </div>
          <div class="field">
            <label class="field-label" for="internalNote">Intern anteckning</label>
            <textarea id="internalNote" class="input" [(ngModel)]="internalNote" rows="3"></textarea>
          </div>
          <button type="button" class="btn-secondary" (click)="saveNotes()">Spara anteckningar</button>
        </div>

        <div class="card">
          <div class="field">
            <label class="field-label" for="startsAt">Ny starttid</label>
            <input id="startsAt" class="input" type="datetime-local" [(ngModel)]="startsAt" />
          </div>
          <button type="button" class="btn-secondary" (click)="reschedule()">Flytta besök</button>
        </div>

        <div class="card">
          <h2 class="section-title">Aviseringar</h2>
          @for (n of notes(); track n.id) {
            <p>{{ n.subject || n.type }} skickad {{ n.createdAt | date:'d MMM HH:mm' }}, {{ deliveryLabel(n.status) }}</p>
          } @empty {
            <p class="muted">Inga aviseringar ännu.</p>
          }
        </div>

        @if (error()) { <div class="alert alert-error">{{ error() }}</div> }

        <app-entity-history entityType="BookingLine" [entityId]="b.id" />

        <div class="actions-bar">
          @if (b.status === 'Requested') {
            <button type="button" class="btn-primary" (click)="act('approve')">Godkänn</button>
          }
          @if (b.status === 'Confirmed') {
            <button class="btn-primary" type="button" (click)="complete()">Klar → journal</button>
            <button type="button" class="btn-secondary" (click)="act('no-show')">Utebliven</button>
          }
          @if (b.status === 'Requested' || b.status === 'Confirmed') {
            <button type="button" class="btn-danger" (click)="cancel()">Avboka</button>
          }
          @if (b.journalEntryId) {
            <a class="btn-secondary" [routerLink]="['/journals', b.journalEntryId, 'edit']">Öppna journal</a>
          }
          <a class="btn-ghost" [routerLink]="['/journals']" [queryParams]="{ horseId: b.horseId }">Journalhistorik</a>
        </div>
      } @else if (error()) {
        <div class="alert alert-error">{{ error() }}</div>
      } @else {
        <p class="loading">Laddar…</p>
      }
    </div>
  `
})
export class BookingDetailComponent implements OnInit {
  private api = inject(Api);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);
  row = signal<BookingListItem | null>(null);
  notes = signal<{ id: string; type: string; subject?: string; status: string; createdAt: string }[]>([]);
  error = signal('');
  clientNote = '';
  internalNote = '';
  startsAt = '';
  statusClass = statusClass;
  statusText = statusText;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.api.get<BookingListItem>(`/api/app/bookings/${id}`).subscribe({
      next: b => {
        this.row.set(b);
        this.clientNote = b.clientNote ?? '';
        this.internalNote = b.internalNote ?? '';
        this.startsAt = toLocalInput(new Date(b.visitStartsAt));
        this.api.get<{ id: string; type: string; subject?: string; status: string; createdAt: string }[]>(`/api/app/bookings/${id}/notifications`)
          .subscribe(n => this.notes.set(n));
      },
      error: () => this.error.set('Bokningen hittades inte.')
    });
  }

  deliveryLabel(status: string): string {
    const s = (status ?? '').toLowerCase();
    if (s === 'delivery' || s === 'delivered') return 'levererad';
    if (s === 'sent') return 'skickad';
    if (s === 'bounce' || s === 'bounced') return 'studsade';
    if (s === 'failed') return 'misslyckad';
    return status;
  }

  format(iso: string): string {
    return new Date(iso).toLocaleString('sv-SE', { weekday: 'long', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }
  time(iso: string): string {
    const d = new Date(iso);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }
  address(b: BookingListItem): string {
    return [b.addressStreet, b.addressPostcode, b.addressCity].filter(Boolean).join(', ');
  }

  saveNotes(): void {
    const b = this.row();
    if (!b) return;
    this.api.patch(`/api/app/bookings/${b.id}`, { clientNote: this.clientNote, internalNote: this.internalNote })
      .subscribe({
        next: () => this.toast.success('Anteckningar sparade.'),
        error: () => this.toast.error('Kunde inte spara.')
      });
  }

  reschedule(): void {
    const b = this.row();
    if (!b) return;
    this.api.patch(`/api/app/visits/${b.visitId}`, { startsAt: new Date(this.startsAt).toISOString() }).subscribe({
      next: () => {
        this.toast.success('Besöket flyttades.');
        this.ngOnInit();
      },
      error: err => this.error.set(err?.error?.message || 'Tiden är inte ledig.')
    });
  }

  act(action: 'approve' | 'no-show'): void {
    const b = this.row();
    if (!b) return;
    const run = () => this.api.post(`/api/app/bookings/${b.id}/${action}`, {}).subscribe({
      next: () => {
        this.toast.success(action === 'approve' ? 'Bokningen godkändes.' : 'Markerad som utebliven.');
        this.ngOnInit();
      },
      error: () => this.error.set('Åtgärden gick inte att utföra.')
    });
    if (action === 'no-show') {
      this.confirm.confirm({
        title: 'Markera som utebliven',
        message: 'Vill du markera den här bokningen som utebliven?',
        confirmLabel: 'Utebliven',
        destructive: true
      }).then(ok => { if (ok) run(); });
      return;
    }
    run();
  }

  async cancel(): Promise<void> {
    const b = this.row();
    if (!b) return;
    const ok = await this.confirm.confirm({
      title: 'Avboka',
      message: 'Vill du avboka den här tiden?',
      confirmLabel: 'Avboka',
      destructive: true
    });
    if (!ok) return;
    this.api.post(`/api/app/bookings/${b.id}/cancel`, { reason: 'Avbokad i admin' }).subscribe({
      next: () => {
        this.toast.success('Bokningen avbokades.');
        this.router.navigate(['/schema']);
      },
      error: () => this.error.set('Kunde inte avboka.')
    });
  }

  complete(): void {
    const b = this.row();
    if (!b) return;
    this.api.post<{ journalId: string }>(`/api/app/bookings/${b.id}/complete`, {}).subscribe({
      next: res => this.router.navigate(['/journals', res.journalId, 'edit']),
      error: () => this.error.set('Kunde inte slutföra.')
    });
  }
}

function toLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
