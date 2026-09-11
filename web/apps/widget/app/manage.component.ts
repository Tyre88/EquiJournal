import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { BookingSummary, publicApi, PublicApiError } from './public-api';

@Component({
  selector: 'app-manage',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="wrap">
      <h1>Din bokning</h1>
      @if (missing()) {
        <p class="alert" role="alert">Länken är ogiltig eller har gått ut.</p>
      } @else if (summary(); as row) {
        <dl>
          <dt>Behandling</dt><dd>{{ row.treatmentName }}</dd>
          <dt>Tid</dt><dd>{{ row.startsAt | date:'yyyy-MM-dd HH:mm':'Europe/Stockholm':'sv-SE' }}</dd>
          <dt>Status</dt><dd>{{ row.status }}</dd>
          <dt>Referens</dt><dd>{{ row.publicReference }}</dd>
        </dl>
        @if (row.cancellationPolicy) { <p>{{ row.cancellationPolicy }}</p> }
        @if (message()) { <p class="alert" role="status">{{ message() }}</p> }
        @if (row.status === 'Requested' || row.status === 'Confirmed') {
          <button type="button" class="btn ghost" (click)="cancel()">Avboka</button>
          @if (slots().length) {
            <h2>Boka om</h2>
            <div class="slots">
              @for (slot of slots(); track slot) {
                <button type="button" class="chip" (click)="reschedule(slot)">{{ slot | date:'yyyy-MM-dd HH:mm':'Europe/Stockholm':'sv-SE' }}</button>
              }
            </div>
          } @else {
            <button type="button" class="btn" (click)="loadSlots()">Visa andra tider</button>
          }
        }
      }
    </div>
  `
})
export class ManageComponent implements OnInit {
  summary = signal<BookingSummary | null>(null);
  missing = signal(false);
  message = signal('');
  slots = signal<string[]>([]);
  private token = '';

  constructor(private route: ActivatedRoute) {}

  async ngOnInit(): Promise<void> {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    try {
      this.summary.set(await publicApi.summary(this.token));
    } catch {
      this.missing.set(true);
    }
  }

  async cancel(): Promise<void> {
    try {
      await publicApi.cancel(this.token);
      this.message.set('Bokningen är avbokad.');
      const current = this.summary();
      if (current) this.summary.set({ ...current, status: 'Cancelled' });
    } catch {
      this.message.set('Kunde inte avboka.');
    }
  }

  async loadSlots(): Promise<void> {
    const row = this.summary();
    if (!row) return;
    try {
      const treatments = await publicApi.treatments();
      const match = treatments.treatments.find(t => t.name === row.treatmentName);
      if (!match) return;
      const from = new Date();
      const to = new Date(from.getTime() + 13 * 86400000);
      const iso = (d: Date) => d.toISOString().slice(0, 10);
      const result = await publicApi.slots(match.id, iso(from), iso(to), row.addressPostcode ?? '');
      this.slots.set(result.starts);
    } catch (err) {
      this.message.set(err instanceof PublicApiError && err.status === 429
        ? 'För många försök. Vänta en stund.'
        : 'Kunde inte hämta tider.');
    }
  }

  async reschedule(startsAt: string): Promise<void> {
    try {
      await publicApi.reschedule(this.token, startsAt);
      this.message.set('Tiden är flyttad.');
      const current = this.summary();
      if (current) this.summary.set({ ...current, startsAt });
    } catch (err) {
      if (err instanceof PublicApiError && err.status === 409) {
        this.slots.set(err.slots ?? []);
        this.message.set('Tiden är inte längre ledig. Välj en ny tid.');
        return;
      }
      this.message.set('Kunde inte boka om.');
    }
  }
}
