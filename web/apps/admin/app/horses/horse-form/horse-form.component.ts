import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../../api';

interface OwnerOption { id: string; name: string; }

@Component({
  selector: 'app-horse-form',
  standalone: true,
  imports: [ReactiveFormsModule, EjPageHeaderComponent],
  template: `
    <div class="page page--narrow">
      <ej-page-header [title]="isEdit() ? 'Redigera häst' : 'Ny häst'" backHref="/horses" />
      <form class="card" [formGroup]="form" (ngSubmit)="save()">
        @if (!isEdit()) {
          <div class="field">
            <label class="field-label required" for="ownerId">Ägare</label>
            <select id="ownerId" class="input" formControlName="ownerId">
              <option value="">Välj kund</option>
              @for (o of owners(); track o.id) {
                <option [value]="o.id">{{ o.name }}</option>
              }
            </select>
          </div>
        }
        <div class="field">
          <label class="field-label required" for="name">Namn</label>
          <input id="name" class="input" formControlName="name" />
        </div>
        <div class="field">
          <label class="field-label" for="species">Art</label>
          <select id="species" class="input" formControlName="species">
            <option value="Hast">Häst</option>
            <option value="Ko">Ko</option>
            <option value="Hund">Hund</option>
            <option value="Katt">Katt</option>
            <option value="other">Övrigt</option>
          </select>
        </div>
        <div class="field"><label class="field-label" for="breed">Ras</label><input id="breed" class="input" formControlName="breed" /></div>
        <div class="field">
          <label class="field-label" for="sex">Kön</label>
          <select id="sex" class="input" formControlName="sex">
            <option value="Sto">Sto</option>
            <option value="Valack">Valack</option>
            <option value="Hingst">Hingst</option>
            <option value="Okant">Okänt</option>
          </select>
        </div>
        <div class="field"><label class="field-label" for="birthYear">Födelseår</label><input id="birthYear" class="input" type="number" formControlName="birthYear" /></div>
        <div class="field">
          <label class="field-label" for="ageGroup">Åldersgrupp</label>
          <select id="ageGroup" class="input" formControlName="ageGroup">
            <option value="">—</option>
            <option value="Föl">Föl</option>
            <option value="Unghäst">Unghäst</option>
            <option value="Vuxen">Vuxen</option>
            <option value="Senior">Senior</option>
          </select>
        </div>
        <div class="field"><label class="field-label" for="identity">Identitet</label><input id="identity" class="input" formControlName="identity" /></div>
        <div class="field"><label class="field-label" for="colour">Färg</label><input id="colour" class="input" formControlName="colour" /></div>
        <div class="field"><label class="field-label" for="markings">Tecken</label><input id="markings" class="input" formControlName="markings" /></div>
        <div class="field"><label class="field-label" for="stableLocation">Stall</label><input id="stableLocation" class="input" formControlName="stableLocation" /></div>
        <div class="field"><label class="field-label" for="stableAddress">Stalladress</label><input id="stableAddress" class="input" formControlName="stableAddress" /></div>
        <div class="field"><label class="field-label" for="stablePostcode">Postnummer</label><input id="stablePostcode" class="input" formControlName="stablePostcode" /></div>
        <div class="field"><label class="field-label" for="stableCity">Ort</label><input id="stableCity" class="input" formControlName="stableCity" /></div>
        <div class="field"><label class="field-label" for="background">Bakgrund</label><textarea id="background" class="input" formControlName="background" rows="3"></textarea></div>
        <div class="field"><label class="field-label" for="followUp">Uppföljning (dagar, tom = behandlingens standard)</label>
          <input id="followUp" class="input" type="number" formControlName="followUpOverrideDays" /></div>
        @if (isEdit()) {
          <div class="field">
            <label class="field-label" for="status">Status</label>
            <select id="status" class="input" formControlName="status">
              <option value="Aktiv">Aktiv</option>
              <option value="Arkiverad">Arkiverad</option>
              <option value="Avliden">Avliden</option>
            </select>
          </div>
        }
        @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
        <div class="actions-bar">
          <button type="button" class="btn-ghost" (click)="router.navigate(['/horses'])">Avbryt</button>
          <button class="btn-primary" type="submit" [disabled]="saving()">Spara</button>
        </div>
      </form>
    </div>
  `
})
export class HorseFormComponent implements OnInit {
  private api = inject(Api);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);
  router = inject(Router);

  isEdit = signal(false);
  saving = signal(false);
  error = signal('');
  owners = signal<OwnerOption[]>([]);
  private id: string | null = null;

  form = this.fb.group({
    ownerId: [''],
    name: ['', Validators.required],
    species: ['Hast'],
    breed: [''],
    sex: ['Okant'],
    birthYear: [null as number | null],
    ageGroup: [''],
    identity: [''],
    colour: [''],
    markings: [''],
    stableLocation: [''],
    stableAddress: [''],
    stablePostcode: [''],
    stableCity: [''],
    background: [''],
    status: ['Aktiv'],
    followUpOverrideDays: [null as number | null]
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!this.id);
    const ownerId = this.route.snapshot.queryParamMap.get('ownerId');
    if (ownerId) this.form.patchValue({ ownerId });
    if (!this.id) {
      this.api.get<OwnerOption[]>('/api/app/owners').subscribe(o => this.owners.set(o));
    } else {
      this.api.get<any>(`/api/app/horses/${this.id}`).subscribe({
        next: h => this.form.patchValue(h),
        error: () => this.error.set('Kunde inte ladda hästen.')
      });
    }
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    if (!v.birthYear && !v.ageGroup) {
      this.error.set('Ange födelseår eller åldersgrupp.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    if (this.id) {
      this.api.put(`/api/app/horses/${this.id}`, v).subscribe({
        next: () => { this.toast.success('Hästen sparades.'); this.router.navigate(['/horses', this.id]); },
        error: () => { this.error.set('Kunde inte spara.'); this.saving.set(false); }
      });
    } else {
      this.api.post<{ id: string }>('/api/app/horses', v).subscribe({
        next: res => { this.toast.success('Hästen skapades.'); this.router.navigate(['/horses', res.id]); },
        error: () => { this.error.set('Kunde inte skapa hästen.'); this.saving.set(false); }
      });
    }
  }
}
