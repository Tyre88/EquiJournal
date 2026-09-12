import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmService, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { AvailabilityRule, Slot, TimeOffItem, TreatmentTypeRef, Zone } from '../bookings/booking.models';
import { ZoneMapEditorComponent } from './zone-map-editor.component';

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const DAY_SV = ['söndag', 'måndag', 'tisdag', 'onsdag', 'torsdag', 'fredag', 'lördag'];

interface PracticeHome {
  latitude?: number | null;
  longitude?: number | null;
}

@Component({
  selector: 'app-availability',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent, ZoneMapEditorComponent],
  template: `
    <div class="page">
      <ej-page-header title="Tillgänglighet" subtitle="Rita täckningszoner, sätt veckoregler och ledig tid." />
      @if (loading()) { <p class="loading">Laddar…</p> }
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }

      <section class="card">
        <h2 class="card-title">Zoner</h2>
        <p class="muted intro">Rita ytor eller cirklar på kartan. En buffert i kilometer räknas in när bokningar matchas mot zonen. Restid läggs på när nästa besök ligger i en annan zon.</p>
        <app-zone-map-editor
          [zones]="zones()"
          [homeLat]="homeLat()"
          [homeLng]="homeLng()"
          (changed)="reloadZones()"
        />
      </section>

      <section class="card">
        <h2 class="card-title">Veckoregler</h2>
        @if (rules().length === 0) {
          <p class="muted">Inga regler ännu. Lägg till de dagar du tar emot bokningar.</p>
        }
        <ul class="item-list">
          @for (r of rules(); track r.id) {
            <li>
              <span class="item-main">
                <strong>{{ dayLabel(r.dayOfWeek) }}</strong>
                <span>{{ formatTime(r.startTime) }}–{{ formatTime(r.endTime) }}</span>
                <em>{{ r.zoneName || 'Alla zoner' }}</em>
              </span>
              <button type="button" class="btn-danger btn-sm" (click)="deleteRule(r.id)">Ta bort</button>
            </li>
          }
        </ul>
        <div class="add-form">
          <div class="field">
            <label class="field-label" for="ruleDay">Dag</label>
            <select id="ruleDay" class="input" [(ngModel)]="newRuleDay">
              @for (d of weekDays; track d.value) {
                <option [value]="d.value">{{ d.label }}</option>
              }
            </select>
          </div>
          <div class="field">
            <label class="field-label" for="ruleStart">Start</label>
            <input id="ruleStart" class="input" type="time" [(ngModel)]="newRuleStart" />
          </div>
          <div class="field">
            <label class="field-label" for="ruleEnd">Slut</label>
            <input id="ruleEnd" class="input" type="time" [(ngModel)]="newRuleEnd" />
          </div>
          <div class="field">
            <label class="field-label" for="ruleZone">Zon</label>
            <select id="ruleZone" class="input" [(ngModel)]="newRuleZone">
              <option value="">Alla zoner</option>
              @for (z of zones(); track z.id) {
                <option [value]="z.id">{{ z.name }}</option>
              }
            </select>
          </div>
          <button type="button" class="btn-primary add-btn" (click)="addRule()">Lägg till regel</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Ledig tid</h2>
        @if (timeOff().length === 0) {
          <p class="muted">Inga lediga perioder inlagda.</p>
        }
        <ul class="item-list">
          @for (t of timeOff(); track t.id) {
            <li>
              <span class="item-main">
                <strong>{{ format(t.startsAt) }}–{{ format(t.endsAt) }}</strong>
                <span>{{ t.reason || 'Ledig' }}</span>
                @if (t.allDay) { <em>Heldag</em> }
                @if (t.recurringAnnual) { <em>Årlig</em> }
              </span>
              <button type="button" class="btn-danger btn-sm" (click)="deleteTimeOff(t.id)">Ta bort</button>
            </li>
          }
        </ul>
        <div class="add-form">
          <div class="field">
            <label class="field-label" for="offDate">Datum</label>
            <input id="offDate" class="input" type="date" [(ngModel)]="offDate" />
          </div>
          @if (!offAllDay) {
            <div class="field">
              <label class="field-label" for="offStart">Från</label>
              <input id="offStart" class="input" type="time" [(ngModel)]="offStartTime" />
            </div>
            <div class="field">
              <label class="field-label" for="offEnd">Till</label>
              <input id="offEnd" class="input" type="time" [(ngModel)]="offEndTime" />
            </div>
          }
          <div class="field">
            <label class="field-label" for="offReason">Anledning</label>
            <input id="offReason" class="input" [(ngModel)]="offReason" placeholder="Valfritt" />
          </div>
          <label class="check"><input type="checkbox" [(ngModel)]="offAllDay" /> Heldag</label>
          <label class="check"><input type="checkbox" [(ngModel)]="offAnnual" /> Årlig</label>
          <button type="button" class="btn-primary add-btn" (click)="addTimeOff()">Lägg till</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Förhandsvisning två veckor</h2>
        <div class="add-form">
          <div class="field">
            <label class="field-label" for="previewTreatment">Behandling</label>
            <select id="previewTreatment" class="input" [(ngModel)]="previewTreatment" (change)="loadPreview()">
              <option value="">Välj behandling</option>
              @for (t of treatments(); track t.id) {
                <option [value]="t.id">{{ t.name }}</option>
              }
            </select>
          </div>
          <div class="field">
            <label class="field-label" for="previewPostcode">Testpostnummer</label>
            <input id="previewPostcode" class="input" [(ngModel)]="previewPostcode" placeholder="27531" (change)="loadPreview()" />
          </div>
        </div>
        <p class="muted">Ange ett postnummer för att se tider som gäller den zonen.</p>
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
    .intro { margin: 0 0 1rem; }
    .item-list { list-style: none; margin: 0 0 1rem; padding: 0; display: flex; flex-direction: column; gap: 0.45rem; }
    .item-list li {
      display: flex; justify-content: space-between; gap: 0.75rem; align-items: center;
      padding: 0.65rem 0.75rem; border: 1px solid var(--color-border); border-radius: 8px;
    }
    .item-main { display: flex; flex-wrap: wrap; gap: 0.45rem 0.85rem; align-items: baseline; }
    .item-main em { color: var(--color-text-muted); font-style: normal; font-size: 0.875rem; }
    .add-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 0.75rem; align-items: end; }
    .add-form .field { margin-bottom: 0; }
    .add-btn { justify-self: start; }
    .check { display: flex; align-items: center; gap: 0.4rem; min-height: var(--tap-min); }
    .slots { display: flex; flex-wrap: wrap; gap: 0.35rem; margin-top: 0.75rem; }
  `]
})
export class AvailabilityComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);
  zones = signal<Zone[]>([]);
  rules = signal<AvailabilityRule[]>([]);
  timeOff = signal<TimeOffItem[]>([]);
  treatments = signal<TreatmentTypeRef[]>([]);
  preview = signal<Slot[]>([]);
  error = signal('');
  loading = signal(true);
  homeLat = signal<number | null>(null);
  homeLng = signal<number | null>(null);
  weekDays = DAYS.slice(1).concat(DAYS[0]).map((value, i) => ({ value, label: DAY_SV[(i + 1) % 7] }));

  newRuleDay = 'Monday';
  newRuleStart = '08:00';
  newRuleEnd = '17:00';
  newRuleZone = '';
  offDate = '';
  offStartTime = '08:00';
  offEndTime = '17:00';
  offReason = '';
  offAllDay = false;
  offAnnual = false;
  previewTreatment = '';
  previewPostcode = '';

  ngOnInit(): void {
    this.reload();
    this.api.get<TreatmentTypeRef[]>('/api/app/treatment-types').subscribe(t => this.treatments.set(t));
    this.api.get<PracticeHome>('/api/app/practice-settings').subscribe(p => {
      this.homeLat.set(p.latitude ?? null);
      this.homeLng.set(p.longitude ?? null);
    });
  }

  reload(): void {
    this.reloadZones();
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

  reloadZones(): void {
    this.loading.set(true);
    this.api.get<Zone[]>('/api/app/zones').subscribe({
      next: z => {
        this.zones.set(z);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda zoner.');
        this.loading.set(false);
      }
    });
    this.api.get<AvailabilityRule[]>('/api/app/availability-rules').subscribe({
      next: r => this.rules.set(r)
    });
  }

  dayLabel(day: string): string {
    const i = DAYS.indexOf(day);
    return i >= 0 ? DAY_SV[i] : day;
  }

  formatTime(value: string): string {
    return value.length >= 5 ? value.slice(0, 5) : value;
  }

  format(iso: string): string {
    return new Date(iso).toLocaleString('sv-SE', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
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
    if (!this.offDate) {
      this.error.set('Välj ett datum.');
      return;
    }
    const start = this.offAllDay ? `${this.offDate}T00:00:00` : `${this.offDate}T${this.offStartTime}:00`;
    const end = this.offAllDay ? `${this.offDate}T23:59:59` : `${this.offDate}T${this.offEndTime}:00`;
    this.api.post('/api/app/time-off', {
      startsAt: new Date(start).toISOString(),
      endsAt: new Date(end).toISOString(),
      allDay: this.offAllDay,
      reason: this.offReason,
      recurringAnnual: this.offAnnual
    }).subscribe({
      next: () => {
        this.offReason = '';
        this.toast.success('Ledig tid sparades.');
        this.reload();
      },
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
      postcode: this.previewPostcode.trim() || undefined,
      applyMinNotice: false
    }).subscribe({
      next: slots => this.preview.set(slots.slice(0, 40)),
      error: () => this.error.set('Kunde inte förhandsvisa tider.')
    });
  }
}
