import { Component, inject, signal, output, Input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastService } from '@equijournal/ui';
import { environment } from '../../../../environments/environment';

const API_URL = environment.apiUrl;

@Component({
  selector: 'app-add-horse',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form class="card" [formGroup]="horseForm" (ngSubmit)="onSubmit()">
      <h3 class="card-title">Registrera ny häst</h3>
      <div class="field">
        <label class="field-label required" for="horseName">Namn</label>
        <input id="horseName" class="input" formControlName="name" />
        @if (name?.invalid && name?.touched) { <span class="field-error">Namn krävs</span> }
      </div>
      <div class="grid2">
        <div class="field">
          <label class="field-label" for="species">Art</label>
          <select id="species" class="input" formControlName="species">
            <option value="Häst">Häst</option>
            <option value="Ko">Ko</option>
            <option value="Hund">Hund</option>
            <option value="Katt">Katt</option>
            <option value="Övrigt">Övrigt</option>
          </select>
        </div>
        <div class="field">
          <label class="field-label" for="breed">Ras</label>
          <input id="breed" class="input" formControlName="breed" />
        </div>
      </div>
      <div class="grid2">
        <div class="field">
          <label class="field-label" for="sex">Kön</label>
          <select id="sex" class="input" formControlName="sex">
            <option value="Sto">Sto</option>
            <option value="Valack">Valack</option>
            <option value="Hingst">Hingst</option>
            <option value="Okänt">Okänt</option>
          </select>
        </div>
        <div class="field">
          <label class="field-label" for="birthYear">Födelseår</label>
          <input id="birthYear" class="input" type="number" inputmode="numeric" formControlName="birthYear" />
        </div>
      </div>
      <div class="field">
        <label class="field-label" for="ageGroup">Åldersgrupp</label>
        <select id="ageGroup" class="input" formControlName="ageGroup">
          <option value="">Välj åldersgrupp…</option>
          <option value="Föl">Föl</option>
          <option value="Unghäst">Unghäst</option>
          <option value="Vuxen">Vuxen</option>
          <option value="Senior">Senior</option>
        </select>
      </div>
      <div class="field">
        <label class="field-label" for="identity">Identitet/chip</label>
        <input id="identity" class="input" formControlName="identity" />
      </div>
      <div class="grid2">
        <div class="field">
          <label class="field-label" for="colour">Färg</label>
          <input id="colour" class="input" formControlName="colour" />
        </div>
        <div class="field">
          <label class="field-label" for="markings">Tecken</label>
          <input id="markings" class="input" formControlName="markings" />
        </div>
      </div>
      <div class="field">
        <label class="field-label" for="stableLocation">Stall</label>
        <input id="stableLocation" class="input" formControlName="stableLocation" />
      </div>
      <div class="field">
        <label class="field-label" for="background">Bakgrund</label>
        <textarea id="background" class="input" formControlName="background" rows="3"></textarea>
      </div>
      @if (errorMessage()) { <div class="alert alert-error">{{ errorMessage() }}</div> }
      <div class="form-actions">
        <button type="button" class="btn-ghost" (click)="onCancel()">Avbryt</button>
        <button type="submit" class="btn-primary" [disabled]="loading()">{{ loading() ? 'Sparar…' : 'Spara häst' }}</button>
      </div>
    </form>
  `,
  styles: [`.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; } @media (max-width: 520px) { .grid2 { grid-template-columns: 1fr; } }`]
})
export class AddHorseComponent {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private toast = inject(ToastService);

  @Input() ownerId = '';
  horseAdded = output<string>();
  cancelled = output<void>();
  loading = signal(false);
  errorMessage = signal('');

  horseForm = this.fb.group({
    name: ['', [Validators.required]],
    species: ['Häst'],
    breed: [''],
    sex: ['Okänt'],
    birthYear: [''],
    ageGroup: [''],
    identity: [''],
    colour: [''],
    markings: [''],
    stableLocation: [''],
    stableAddress: [''],
    stablePostcode: [''],
    stableCity: [''],
    background: ['']
  });

  onSubmit(): void {
    if (this.horseForm.invalid) {
      this.horseForm.markAllAsTouched();
      return;
    }
    const birthYearValue = this.horseForm.value.birthYear;
    const ageGroupValue = this.horseForm.value.ageGroup;
    if (!birthYearValue && !ageGroupValue) {
      this.errorMessage.set('Ange födelseår eller åldersgrupp.');
      return;
    }
    this.loading.set(true);
    this.errorMessage.set('');
    const formValue = this.horseForm.value;
    const payload = {
      ownerId: this.ownerId,
      name: formValue.name,
      species: formValue.species === 'Häst' ? 'Hast' : (formValue.species || 'Hast'),
      breed: formValue.breed ?? '',
      sex: formValue.sex === 'Okänt' ? 'Okant' : (formValue.sex || 'Okant'),
      birthYear: birthYearValue ? parseInt(birthYearValue, 10) : null,
      ageGroup: ageGroupValue ?? null,
      identity: formValue.identity ?? '',
      colour: formValue.colour ?? '',
      markings: formValue.markings ?? '',
      stableLocation: formValue.stableLocation ?? '',
      stableAddress: formValue.stableAddress ?? '',
      stablePostcode: formValue.stablePostcode ?? '',
      stableCity: formValue.stableCity ?? '',
      background: formValue.background ?? ''
    };
    this.http.post(`${API_URL}/api/app/horses`, payload).subscribe({
      next: () => {
        this.toast.success('Hästen har lagts till.');
        this.horseForm.reset({ species: 'Häst', sex: 'Okänt' });
        this.horseAdded.emit('success');
      },
      error: () => {
        this.errorMessage.set('Kunde inte spara hästen. Försök igen.');
        this.loading.set(false);
      }
    });
  }

  onCancel(): void {
    this.horseForm.reset();
    this.cancelled.emit();
  }

  get name() { return this.horseForm.get('name'); }
}
