import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { AddressPickerComponent, AddressPick } from './address-picker.component';

interface Practice {
  name: string;
  clinic: string;
  address: string;
  addressStreet: string;
  addressPostcode: string;
  addressCity: string;
  latitude?: number | null;
  longitude?: number | null;
  phone: string;
  email: string;
  mapsBrowserKey?: string;
}

@Component({
  selector: 'app-practice-settings',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent, AddressPickerComponent],
  template: `
    <ej-page-header title="Verksamhet" subtitle="Uppgifter som syns i mejl, kalenderfil, PDF och dagens rutt." />
    @if (error()) {
      <div class="alert alert-error">{{ error() }}</div>
    }
    @if (s(); as p) {
      <div class="card">
        <h2 class="section-title">Kontakt</h2>
        <div class="field"><label class="field-label" for="name">Namn</label><input id="name" class="input" [(ngModel)]="p.name" /></div>
        <div class="field"><label class="field-label" for="clinic">Klinik</label><input id="clinic" class="input" [(ngModel)]="p.clinic" /></div>
        <div class="field"><label class="field-label" for="phone">Telefon</label><input id="phone" class="input" type="tel" [(ngModel)]="p.phone" /></div>
        <div class="field"><label class="field-label" for="email">E-post</label><input id="email" class="input" type="email" [(ngModel)]="p.email" /></div>
      </div>
      <div class="card">
        <h2 class="section-title">Hemadress</h2>
        <p class="muted">Startpunkt för dagens schema och körsträckor. Används också i mejl och kalenderfil.</p>
        <app-address-picker
          [apiKey]="p.mapsBrowserKey || ''"
          [latitude]="p.latitude ?? null"
          [longitude]="p.longitude ?? null"
          (picked)="applyAddress($event)"
        />
        <div class="field">
          <label class="field-label" for="addressStreet">Gatuadress</label>
          <input id="addressStreet" class="input" [(ngModel)]="p.addressStreet" autocomplete="street-address" />
        </div>
        <div class="grid2">
          <div class="field">
            <label class="field-label" for="addressPostcode">Postnummer</label>
            <input id="addressPostcode" class="input" [(ngModel)]="p.addressPostcode" autocomplete="postal-code" />
          </div>
          <div class="field">
            <label class="field-label" for="addressCity">Ort</label>
            <input id="addressCity" class="input" [(ngModel)]="p.addressCity" autocomplete="address-level2" />
          </div>
        </div>
        <div class="grid2">
          <div class="field">
            <label class="field-label" for="latitude">Latitud</label>
            <input id="latitude" class="input" type="number" step="0.000001" [(ngModel)]="p.latitude" />
          </div>
          <div class="field">
            <label class="field-label" for="longitude">Longitud</label>
            <input id="longitude" class="input" type="number" step="0.000001" [(ngModel)]="p.longitude" />
          </div>
        </div>
        <button type="button" class="btn-primary" (click)="save()">Spara</button>
      </div>
    }
  `,
  styles: [`
    .section-title { margin: 0 0 1rem; color: var(--color-primary-text); }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .card + .card { margin-top: 1rem; }
    .muted { margin: 0 0 1rem; }
    @media (max-width: 520px) { .grid2 { grid-template-columns: 1fr; } }
  `]
})
export class PracticeSettingsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  s = signal<Practice | null>(null);
  error = signal('');

  ngOnInit(): void {
    this.api.get<Practice>('/api/app/practice-settings').subscribe({
      next: v => this.s.set(this.normalize(v)),
      error: () => this.error.set('Kunde inte ladda verksamhetsuppgifter.')
    });
  }

  applyAddress(pick: AddressPick): void {
    this.s.update(current => current ? {
      ...current,
      addressStreet: pick.street,
      addressPostcode: pick.postcode,
      addressCity: pick.city,
      latitude: pick.latitude,
      longitude: pick.longitude
    } : current);
  }

  save(): void {
    const current = this.s();
    if (!current) return;
    this.api.put<Practice>('/api/app/practice-settings', current).subscribe({
      next: v => {
        this.s.set(this.normalize(v));
        this.toast.success('Verksamheten sparades.');
      },
      error: () => this.toast.error('Kunde inte spara.')
    });
  }

  private normalize(value: Practice): Practice {
    return {
      name: value.name ?? '',
      clinic: value.clinic ?? '',
      address: value.address ?? '',
      addressStreet: value.addressStreet || value.address || '',
      addressPostcode: value.addressPostcode ?? '',
      addressCity: value.addressCity ?? '',
      latitude: value.latitude ?? null,
      longitude: value.longitude ?? null,
      phone: value.phone ?? '',
      email: value.email ?? '',
      mapsBrowserKey: value.mapsBrowserKey ?? ''
    };
  }
}
