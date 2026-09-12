import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { environment } from '../../../environments/environment';
import { AddressPickerComponent, AddressPick } from '../../settings/address-picker.component';

const API_URL = environment.apiUrl;

@Component({
  selector: 'app-owner-form',
  standalone: true,
  imports: [ReactiveFormsModule, EjPageHeaderComponent, AddressPickerComponent],
  template: `
    <div class="page page--narrow">
      <ej-page-header [title]="isEdit() ? 'Redigera kund' : 'Ny kund'" backHref="/owners" />
      <form class="card" [formGroup]="ownerForm" (ngSubmit)="onSubmit()">
        <h2 class="card-title">Grundinformation</h2>
        <div class="field">
          <label class="field-label required" for="name">Namn</label>
          <input id="name" class="input" type="text" formControlName="name" />
          @if (name?.invalid && name?.touched) { <span class="field-error">Namn krävs</span> }
        </div>
        <div class="field">
          <label class="field-label required" for="email">E-post</label>
          <input id="email" class="input" type="email" autocomplete="email" formControlName="email" />
          @if (email?.invalid && email?.touched) {
            <span class="field-error">{{ email?.errors?.['required'] ? 'E-post krävs' : 'Ange en giltig e-postadress' }}</span>
          }
        </div>
        <div class="field">
          <label class="field-label" for="phone">Telefon</label>
          <input id="phone" class="input" type="tel" autocomplete="tel" formControlName="phone" />
        </div>

        <h2 class="card-title">Adress</h2>
        <app-address-picker
          [apiKey]="mapsBrowserKey()"
          [latitude]="mapLatitude()"
          [longitude]="mapLongitude()"
          ariaLabel="Karta för kundadress"
          pinLabel="K"
          (picked)="applyAddress($event)"
        />
        <div class="field">
          <label class="field-label" for="addressStreet">Gatuadress</label>
          <input id="addressStreet" class="input" formControlName="addressStreet" />
        </div>
        <div class="grid2">
          <div class="field">
            <label class="field-label" for="addressPostcode">Postnummer</label>
            <input id="addressPostcode" class="input" formControlName="addressPostcode" />
          </div>
          <div class="field">
            <label class="field-label" for="addressCity">Ort</label>
            <input id="addressCity" class="input" formControlName="addressCity" />
          </div>
        </div>
        <div class="grid2">
          <div class="field">
            <label class="field-label" for="latitude">Latitud</label>
            <input id="latitude" class="input" type="number" step="0.000001" formControlName="latitude" />
          </div>
          <div class="field">
            <label class="field-label" for="longitude">Longitud</label>
            <input id="longitude" class="input" type="number" step="0.000001" formControlName="longitude" />
          </div>
        </div>

        <h2 class="card-title">Övrigt</h2>
        <div class="field">
          <label class="field-label" for="notes">Anteckningar</label>
          <textarea id="notes" class="input" formControlName="notes" rows="4"></textarea>
        </div>
        <label class="check">
          <input type="checkbox" formControlName="marketingConsent" />
          Samtycke till marknadsföring
        </label>

        @if (errorMessage()) { <div class="alert alert-error">{{ errorMessage() }}</div> }

        <div class="actions-bar">
          <button type="button" class="btn-ghost" (click)="onCancel()">Avbryt</button>
          <button type="submit" class="btn-primary" [disabled]="loading()">{{ loading() ? 'Sparar…' : 'Spara' }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .check { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
    @media (max-width: 520px) { .grid2 { grid-template-columns: 1fr; } }
  `]
})
export class OwnerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private toast = inject(ToastService);

  private ownerId = signal<string | null>(null);
  isEdit = signal(false);
  loading = signal(false);
  errorMessage = signal('');
  mapsBrowserKey = signal('');
  mapLatitude = signal<number | null>(null);
  mapLongitude = signal<number | null>(null);

  ownerForm = this.fb.group({
    name: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    addressStreet: [''],
    addressPostcode: [''],
    addressCity: [''],
    notes: [''],
    marketingConsent: [false],
    latitude: [null as number | null],
    longitude: [null as number | null]
  });

  ngOnInit(): void {
    this.http.get<{ mapsBrowserKey?: string }>(`${API_URL}/api/app/practice-settings`).subscribe({
      next: p => this.mapsBrowserKey.set(p.mapsBrowserKey ?? ''),
      error: () => this.mapsBrowserKey.set('')
    });
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.ownerId.set(id);
      this.isEdit.set(true);
      this.fetchOwner(id);
    }
  }

  fetchOwner(id: string): void {
    this.loading.set(true);
    this.http.get<any>(`${API_URL}/api/app/owners/${id}`).subscribe({
      next: (owner) => {
        this.ownerForm.patchValue({
          name: owner.name ?? '',
          email: owner.email ?? '',
          phone: owner.phone ?? '',
          addressStreet: owner.addressStreet ?? '',
          addressPostcode: owner.addressPostcode ?? '',
          addressCity: owner.addressCity ?? '',
          notes: owner.notes ?? '',
          marketingConsent: owner.marketingConsent ?? false,
          latitude: owner.latitude ?? null,
          longitude: owner.longitude ?? null
        });
        this.mapLatitude.set(owner.latitude ?? null);
        this.mapLongitude.set(owner.longitude ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Kunde inte ladda kundinformation.');
        this.loading.set(false);
      }
    });
  }

  applyAddress(pick: AddressPick): void {
    this.ownerForm.patchValue({
      addressStreet: pick.street,
      addressPostcode: pick.postcode,
      addressCity: pick.city,
      latitude: pick.latitude,
      longitude: pick.longitude
    });
    this.mapLatitude.set(pick.latitude);
    this.mapLongitude.set(pick.longitude);
  }

  onSubmit(): void {
    if (this.ownerForm.invalid) {
      this.ownerForm.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.errorMessage.set('');
    const formValue = this.ownerForm.value;
    const payload = {
      name: formValue.name,
      email: formValue.email,
      phone: formValue.phone ?? '',
      addressStreet: formValue.addressStreet ?? '',
      addressPostcode: formValue.addressPostcode ?? '',
      addressCity: formValue.addressCity ?? '',
      notes: formValue.notes ?? '',
      marketingConsent: formValue.marketingConsent ?? false,
      latitude: formValue.latitude ?? null,
      longitude: formValue.longitude ?? null
    };
    const ownerId = this.ownerId();
    const req = ownerId
      ? this.http.put(`${API_URL}/api/app/owners/${ownerId}`, payload)
      : this.http.post(`${API_URL}/api/app/owners`, payload);
    req.subscribe({
      next: () => {
        this.toast.success(ownerId ? 'Kunden har sparats.' : 'Kunden har skapats.');
        this.router.navigate(['/owners']);
      },
      error: () => {
        this.errorMessage.set('Kunde inte spara kunden. Försök igen.');
        this.loading.set(false);
      }
    });
  }

  onCancel(): void {
    this.router.navigate(['/owners']);
  }

  get name() { return this.ownerForm.get('name'); }
  get email() { return this.ownerForm.get('email'); }
}
