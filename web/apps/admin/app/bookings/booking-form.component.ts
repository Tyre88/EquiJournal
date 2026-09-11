import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { Slot, TreatmentTypeRef } from './booking.models';

interface OwnerHit { id: string; name: string; email: string; phone?: string; }
interface HorseHit { id: string; name: string; }

@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, EjPageHeaderComponent],
  template: `
    <div class="page page--narrow">
      <ej-page-header title="Ny bokning" backHref="/schema" />
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="field">
          <label class="field-label" for="ownerSearch">Sök kund</label>
          <input id="ownerSearch" class="input" [value]="ownerQuery()" (input)="searchOwners($event)" placeholder="Namn, e-post, telefon" />
        </div>
        @if (owners().length) {
          <ul class="hits">
            @for (o of owners(); track o.id) {
              <li><button type="button" class="btn-secondary" (click)="pickOwner(o)">{{ o.name }} · {{ o.email }}</button></li>
            }
          </ul>
        }
        @if (!selectedOwner()) {
          <fieldset class="card" style="margin-top:0">
            <legend>Eller skapa kund</legend>
            <div class="field"><label class="field-label" for="newOwnerName">Namn</label><input id="newOwnerName" class="input" formControlName="newOwnerName" /></div>
            <div class="field"><label class="field-label" for="newOwnerEmail">E-post</label><input id="newOwnerEmail" class="input" type="email" formControlName="newOwnerEmail" /></div>
            <div class="field"><label class="field-label" for="newOwnerPhone">Telefon</label><input id="newOwnerPhone" class="input" type="tel" formControlName="newOwnerPhone" /></div>
            <div class="field"><label class="field-label" for="newOwnerStreet">Adress</label><input id="newOwnerStreet" class="input" formControlName="newOwnerStreet" /></div>
            <div class="field"><label class="field-label" for="newOwnerPostcode">Postnummer</label><input id="newOwnerPostcode" class="input" formControlName="newOwnerPostcode" /></div>
            <div class="field"><label class="field-label" for="newOwnerCity">Ort</label><input id="newOwnerCity" class="input" formControlName="newOwnerCity" /></div>
            <button type="button" class="btn-secondary" (click)="createOwner()">Spara kund</button>
          </fieldset>
        } @else {
          <p>Kund: <strong>{{ selectedOwner()!.name }}</strong></p>
        }

        @if (horses().length) {
          <div class="field">
            <label class="field-label" for="horseId">Häst</label>
            <select id="horseId" class="input" formControlName="horseId">
              <option value="">Välj häst</option>
              @for (h of horses(); track h.id) {
                <option [value]="h.id">{{ h.name }}</option>
              }
            </select>
          </div>
        }
        @if (selectedOwner()) {
          <fieldset class="card">
            <legend>Ny häst</legend>
            <div class="field"><label class="field-label" for="newHorseName">Namn</label><input id="newHorseName" class="input" formControlName="newHorseName" /></div>
            <div class="field"><label class="field-label" for="newHorseBirthYear">Födelseår</label><input id="newHorseBirthYear" class="input" inputmode="numeric" formControlName="newHorseBirthYear" /></div>
            <button type="button" class="btn-secondary" (click)="createHorse()">Spara häst</button>
          </fieldset>
        }

        <div class="field">
          <label class="field-label" for="treatmentTypeId">Behandling</label>
          <select id="treatmentTypeId" class="input" formControlName="treatmentTypeId">
            <option value="">Välj behandling</option>
            @for (t of treatments(); track t.id) {
              <option [value]="t.id">{{ t.name }} ({{ t.durationMinutes }} min)</option>
            }
          </select>
        </div>
        <div class="field">
          <label class="field-label" for="clientNote">Anteckning från kund</label>
          <textarea id="clientNote" class="input" formControlName="clientNote" rows="2"></textarea>
        </div>
        <div class="field">
          <label class="field-label" for="startsAt">Starttid</label>
          <input id="startsAt" class="input" type="datetime-local" formControlName="startsAt" />
        </div>
        <button type="button" class="btn-secondary" (click)="loadSlots()" [disabled]="!form.value.treatmentTypeId">
          Föreslå lediga tider
        </button>
        @if (slots().length) {
          <div class="slots">
            @for (s of slots(); track s.startsAt) {
              <button type="button" class="btn-ghost" (click)="pickSlot(s)">{{ format(s.startsAt) }}</button>
            }
          </div>
        }
        @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
        <div class="actions-bar">
          <a routerLink="/schema" class="btn-ghost">Avbryt</a>
          <button type="submit" class="btn-primary" [disabled]="form.invalid || saving()">Boka</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    form { display: grid; gap: 0.25rem; }
    fieldset { border: 0; padding: 1rem; }
    legend { font-weight: 700; }
    .hits { list-style: none; padding: 0; margin: 0 0 1rem; display: grid; gap: 0.35rem; }
    .slots { display: grid; grid-template-columns: 1fr 1fr; gap: 0.4rem; margin: 0.75rem 0; }
  `]
})
export class BookingFormComponent implements OnInit {
  private api = inject(Api);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);

  form = this.fb.nonNullable.group({
    horseId: ['', Validators.required],
    treatmentTypeId: ['', Validators.required],
    clientNote: [''],
    startsAt: ['', Validators.required],
    newOwnerName: [''],
    newOwnerEmail: [''],
    newOwnerPhone: [''],
    newOwnerStreet: [''],
    newOwnerPostcode: [''],
    newOwnerCity: [''],
    newHorseName: [''],
    newHorseBirthYear: ['']
  });

  ownerQuery = signal('');
  owners = signal<OwnerHit[]>([]);
  selectedOwner = signal<OwnerHit | null>(null);
  horses = signal<HorseHit[]>([]);
  treatments = signal<TreatmentTypeRef[]>([]);
  slots = signal<Slot[]>([]);
  error = signal('');
  saving = signal(false);

  ngOnInit(): void {
    const starts = this.route.snapshot.queryParamMap.get('startsAt');
    if (starts) this.form.patchValue({ startsAt: toLocalInput(new Date(starts)) });
    this.api.get<TreatmentTypeRef[]>('/api/app/treatment-types').subscribe({
      next: t => this.treatments.set(t),
      error: () => this.error.set('Kunde inte ladda behandlingstyper.')
    });
  }

  searchOwners(ev: Event): void {
    const q = (ev.target as HTMLInputElement).value;
    this.ownerQuery.set(q);
    if (q.trim().length < 2) { this.owners.set([]); return; }
    this.api.post<OwnerHit[]>('/api/app/owners/search', { query: q }).subscribe(list => this.owners.set(list));
  }

  pickOwner(owner: OwnerHit): void {
    this.selectedOwner.set(owner);
    this.owners.set([]);
    this.api.get<HorseHit[]>(`/api/app/horses/by-owner/${owner.id}`).subscribe(h => this.horses.set(h));
  }

  createOwner(): void {
    const v = this.form.getRawValue();
    this.api.post<{ id: string }>('/api/app/owners', {
      name: v.newOwnerName,
      email: v.newOwnerEmail,
      phone: v.newOwnerPhone,
      addressStreet: v.newOwnerStreet,
      addressPostcode: v.newOwnerPostcode,
      addressCity: v.newOwnerCity
    }).subscribe({
      next: res => {
        this.toast.success('Kunden skapades.');
        this.pickOwner({ id: res.id, name: v.newOwnerName, email: v.newOwnerEmail, phone: v.newOwnerPhone });
      },
      error: () => this.error.set('Kunde inte skapa kund.')
    });
  }

  createHorse(): void {
    const owner = this.selectedOwner();
    if (!owner) return;
    const v = this.form.getRawValue();
    this.api.post<{ id: string }>('/api/app/horses', {
      ownerId: owner.id,
      name: v.newHorseName,
      birthYear: Number(v.newHorseBirthYear) || new Date().getFullYear() - 8
    }).subscribe({
      next: res => {
        this.horses.update(list => [...list, { id: res.id, name: v.newHorseName }]);
        this.form.patchValue({ horseId: res.id });
        this.toast.success('Hästen skapades.');
      },
      error: () => this.error.set('Kunde inte skapa häst.')
    });
  }

  loadSlots(): void {
    const treatmentId = this.form.value.treatmentTypeId;
    if (!treatmentId) return;
    const from = new Date();
    const to = new Date();
    to.setDate(to.getDate() + 14);
    this.api.get<Slot[]>('/api/app/availability/slots', {
      treatmentTypeId: treatmentId,
      from: toIsoDate(from),
      to: toIsoDate(to),
      applyMinNotice: false
    }).subscribe({
      next: slots => this.slots.set(slots.slice(0, 24)),
      error: () => this.error.set('Kunde inte hämta tider.')
    });
  }

  pickSlot(slot: Slot): void {
    this.form.patchValue({ startsAt: toLocalInput(new Date(slot.startsAt)) });
  }

  format(iso: string): string {
    return new Date(iso).toLocaleString('sv-SE', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    this.api.post('/api/app/bookings', {
      startsAt: new Date(v.startsAt).toISOString(),
      lines: [{ horseId: v.horseId, treatmentTypeId: v.treatmentTypeId, clientNote: v.clientNote }]
    }).subscribe({
      next: () => {
        this.toast.success('Bokningen skapades.');
        this.router.navigate(['/schema']);
      },
      error: err => {
        this.error.set(err?.error?.message || 'Kunde inte boka.');
        if (err?.error?.slots) this.slots.set(err.error.slots);
        this.saving.set(false);
      }
    });
  }
}

function toLocalInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
