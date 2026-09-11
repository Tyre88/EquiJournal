import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmService, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { AvailabilityRule, Slot, TimeOffItem, TreatmentTypeRef, Zone } from '../bookings/booking.models';

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const DAY_SV = ['sön', 'mån', 'tis', 'ons', 'tor', 'fre', 'lör'];

@Component({
  selector: 'app-availability',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header title="Tillgänglighet" subtitle="Zoner, veckoregler och ledig tid." />
      @if (loading()) { <p class="loading">Laddar…</p> }
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }

      <section class="card">
        <h2 class="card-title">Zoner</h2>
        @for (z of zones(); track z.id) {
          <div class="row">
            <strong>{{ z.name }}</strong>
            <label class="sr">Postnummer
              <input class="input" [(ngModel)]="z.postcodesText" placeholder="Postnummer, kommaseparerade" [disabled]="z.isFallback" />
            </label>
            <label class="sr">Restid
              <input class="input" type="number" [(ngModel)]="z.travelBufferMinutes" placeholder="Restid min" />
            </label>
            <button type="button" class="btn-secondary" (click)="saveZone(z)">Spara</button>
            @if (!z.isFallback) {
              <button type="button" class="btn-danger" (click)="deleteZone(z.id)">Ta bort</button>
            }
          </div>
        }
        <div class="row">
          <label class="sr">Ny zon<input class="input" [(ngModel)]="newZoneName" placeholder="Ny zon" /></label>
          <label class="sr">Postnummer<input class="input" [(ngModel)]="newZonePostcodes" placeholder="27531, 27350" /></label>
          <button type="button" class="btn-primary" (click)="addZone()">Lägg till zon</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Veckoregler</h2>
        @for (r of rules(); track r.id) {
          <div class="row">
            <span>{{ dayLabel(r.dayOfWeek) }} {{ r.startTime }}–{{ r.endTime }}</span>
            @if (r.zoneName) { <em>{{ r.zoneName }}</em> }
            <button type="button" class="btn-danger btn-sm" (click)="deleteRule(r.id)">Ta bort</button>
          </div>
        }
        <div class="row">
          <label class="sr">Dag
            <select class="input" [(ngModel)]="newRuleDay">
              @for (d of weekDays; track d.value) {
                <option [value]="d.value">{{ d.label }}</option>
              }
            </select>
          </label>
          <label class="sr">Start<input class="input" type="time" [(ngModel)]="newRuleStart" /></label>
          <label class="sr">Slut<input class="input" type="time" [(ngModel)]="newRuleEnd" /></label>
          <label class="sr">Zon
            <select class="input" [(ngModel)]="newRuleZone">
              <option value="">Alla zoner</option>
              @for (z of zones(); track z.id) {
                <option [value]="z.id">{{ z.name }}</option>
              }
            </select>
          </label>
          <button type="button" class="btn-primary" (click)="addRule()">Lägg till regel</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Ledig tid</h2>
        @for (t of timeOff(); track t.id) {
          <div class="row">
            <span>{{ format(t.startsAt) }}–{{ format(t.endsAt) }} {{ t.reason }}</span>
            <button type="button" class="btn-danger btn-sm" (click)="deleteTimeOff(t.id)">Ta bort</button>
          </div>
        }
        <div class="row">
          <label class="sr">Från<input class="input" type="datetime-local" [(ngModel)]="offStart" /></label>
          <label class="sr">Till<input class="input" type="datetime-local" [(ngModel)]="offEnd" /></label>
          <label class="sr">Anledning<input class="input" [(ngModel)]="offReason" placeholder="Anledning" /></label>
          <label><input type="checkbox" [(ngModel)]="offAllDay" /> Heldag</label>
          <label><input type="checkbox" [(ngModel)]="offAnnual" /> Årlig</label>
          <button type="button" class="btn-primary" (click)="addTimeOff()">Lägg till</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Förhandsvisning två veckor</h2>
        <label class="field-label" for="previewTreatment">Behandling</label>
        <select id="previewTreatment" class="input" [(ngModel)]="previewTreatment" (change)="loadPreview()">
          <option value="">Välj behandling</option>
          @for (t of treatments(); track t.id) {
            <option [value]="t.id">{{ t.name }}</option>
          }
        </select>
        <div class="slots">
          @for (s of preview(); track s.startsAt) {
            <span class="badge">{{ format(s.startsAt) }}</span>
          }
        </div>
        @if (previewTreatment && preview().length === 0 && !loading()) {
          <p class="muted">Inga tider de kommande två veckorna.</p>
        }
      </section>
    </div>
  `,
  styles: [`
    .row { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; margin-bottom: 0.6rem; }
    .sr { display: flex; flex-direction: column; gap: 0.2rem; font-size: 0.75rem; font-weight: 600; color: var(--color-text-muted); min-width: 140px; flex: 1; }
    .slots { display: flex; flex-wrap: wrap; gap: 0.35rem; margin-top: 0.75rem; }
  `]
})
export class AvailabilityComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);
  zones = signal<Array<Zone & { postcodesText?: string }>>([]);
  rules = signal<AvailabilityRule[]>([]);
  timeOff = signal<TimeOffItem[]>([]);
  treatments = signal<TreatmentTypeRef[]>([]);
  preview = signal<Slot[]>([]);
  error = signal('');
  loading = signal(true);
  weekDays = DAYS.slice(1).concat(DAYS[0]).map((value, i) => ({ value, label: DAY_SV[(i + 1) % 7] }));

  newZoneName = '';
  newZonePostcodes = '';
  newRuleDay = 'Monday';
  newRuleStart = '08:00';
  newRuleEnd = '17:00';
  newRuleZone = '';
  offStart = '';
  offEnd = '';
  offReason = '';
  offAllDay = false;
  offAnnual = false;
  previewTreatment = '';

  ngOnInit(): void {
    this.reload();
    this.api.get<TreatmentTypeRef[]>('/api/app/treatment-types').subscribe(t => this.treatments.set(t));
  }

  reload(): void {
    this.loading.set(true);
    this.api.get<Zone[]>('/api/app/zones').subscribe({
      next: z => {
        this.zones.set(z.map(x => ({ ...x, postcodesText: (x.postcodes ?? []).join(', ') })));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda zoner.');
        this.loading.set(false);
      }
    });
    this.api.get<AvailabilityRule[]>('/api/app/availability-rules').subscribe({
      next: r => this.rules.set(r),
      error: () => this.error.set('Kunde inte ladda regler.')
    });
    this.api.get<TimeOffItem[]>('/api/app/time-off').subscribe({
      next: t => this.timeOff.set(t),
      error: () => this.error.set('Kunde inte ladda ledig tid.')
    });
    if (this.previewTreatment) this.loadPreview();
  }

  dayLabel(day: string): string {
    const i = DAYS.indexOf(day);
    return i >= 0 ? DAY_SV[i] : day;
  }

  format(iso: string): string {
    return new Date(iso).toLocaleString('sv-SE', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  saveZone(z: Zone & { postcodesText?: string }): void {
    const postcodes = (z.postcodesText ?? '').split(',').map(s => s.trim()).filter(Boolean);
    this.api.put(`/api/app/zones/${z.id}`, {
      name: z.name,
      postcodes,
      travelBufferMinutes: z.travelBufferMinutes
    }).subscribe({
      next: () => { this.toast.success('Zonen sparades.'); this.reload(); },
      error: () => this.error.set('Kunde inte spara zon.')
    });
  }

  addZone(): void {
    this.api.post('/api/app/zones', {
      name: this.newZoneName,
      postcodes: this.newZonePostcodes.split(',').map(s => s.trim()).filter(Boolean)
    }).subscribe({
      next: () => { this.newZoneName = ''; this.newZonePostcodes = ''; this.toast.success('Zonen skapades.'); this.reload(); },
      error: () => this.error.set('Kunde inte skapa zon.')
    });
  }

  async deleteZone(id: string): Promise<void> {
    const ok = await this.confirm.confirm({ title: 'Ta bort zon', message: 'Zonen tas bort permanent.', confirmLabel: 'Ta bort', destructive: true });
    if (!ok) return;
    this.api.delete(`/api/app/zones/${id}`).subscribe({
      next: () => { this.toast.success('Zonen togs bort.'); this.reload(); },
      error: () => this.error.set('Kunde inte ta bort zonen.')
    });
  }

  addRule(): void {
    this.api.post('/api/app/availability-rules', {
      dayOfWeek: this.newRuleDay,
      startTime: this.newRuleStart.length === 5 ? `${this.newRuleStart}:00` : this.newRuleStart,
      endTime: this.newRuleEnd.length === 5 ? `${this.newRuleEnd}:00` : this.newRuleEnd,
      zoneId: this.newRuleZone || null,
      active: true
    }).subscribe({
      next: () => { this.toast.success('Regeln sparades.'); this.reload(); },
      error: () => this.error.set('Kunde inte spara regel.')
    });
  }

  async deleteRule(id: string): Promise<void> {
    const ok = await this.confirm.confirm({ title: 'Ta bort regel', message: 'Veckoregeln tas bort.', confirmLabel: 'Ta bort', destructive: true });
    if (!ok) return;
    this.api.delete(`/api/app/availability-rules/${id}`).subscribe({ next: () => this.reload() });
  }

  addTimeOff(): void {
    this.api.post('/api/app/time-off', {
      startsAt: new Date(this.offStart).toISOString(),
      endsAt: new Date(this.offEnd).toISOString(),
      allDay: this.offAllDay,
      reason: this.offReason,
      recurringAnnual: this.offAnnual
    }).subscribe({
      next: () => { this.toast.success('Ledig tid sparades.'); this.reload(); },
      error: () => this.error.set('Kunde inte spara ledig tid.')
    });
  }

  async deleteTimeOff(id: string): Promise<void> {
    const ok = await this.confirm.confirm({ title: 'Ta bort ledig tid', message: 'Posten tas bort.', confirmLabel: 'Ta bort', destructive: true });
    if (!ok) return;
    this.api.delete(`/api/app/time-off/${id}`).subscribe({ next: () => this.reload() });
  }

  loadPreview(): void {
    if (!this.previewTreatment) { this.preview.set([]); return; }
    const from = new Date();
    const to = new Date();
    to.setDate(to.getDate() + 14);
    const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.api.get<Slot[]>('/api/app/availability/slots', {
      treatmentTypeId: this.previewTreatment,
      from: iso(from),
      to: iso(to),
      applyMinNotice: false
    }).subscribe({
      next: slots => this.preview.set(slots.slice(0, 40)),
      error: () => this.error.set('Kunde inte förhandsvisa tider.')
    });
  }
}
