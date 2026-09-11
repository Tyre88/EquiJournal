import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { TemplateBuilderComponent } from '../template-builder/template-builder.component';

interface TreatmentType {
  id: string;
  name: string;
  slug?: string;
  shortDescription: string;
  publicDescription?: string;
  durationMinutes: number;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  colour?: string;
  priceExclVat: number;
  priceExcludingVat?: number;
  vatRate: number;
  bookableOnline: boolean;
  requiresApproval: boolean;
  minNoticeHours: number;
  maxAdvanceDays: number;
  allowedLocationTypes?: string;
  followUpIntervalDays?: number | null;
  journalTemplateJson?: string;
  isActive: boolean;
}

@Component({
  selector: 'app-treatment-type-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TemplateBuilderComponent, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header [title]="isEdit ? 'Redigera behandlingstyp' : 'Ny behandlingstyp'" backHref="/treatment-types" />
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }

      <form [formGroup]="treatmentForm" (ngSubmit)="onSubmit()" class="form-container">
        <div class="form-sections">
          <!-- Grundläggande -->
          <div class="section">
            <h2 class="section-title">Grundläggande</h2>
            <div class="form-row">
              <div class="form-group">
                <label for="name">Namn <span class="required">*</span></label>
                <input
                  id="name"
                  type="text"
                  formControlName="name"
                  placeholder="T.ex. Massering"
                />
                @if (name?.invalid && name?.touched) {
                  <span class="error">Namn krävs</span>
                }
              </div>
              <div class="form-group">
                <label for="slug">Webbadress (slug)</label>
                <input id="slug" type="text" formControlName="slug" placeholder="massage" />
              </div>
            </div>
            <div class="form-row">
              <div class="form-group full-width">
                <label for="shortDescription">Kort beskrivning <span class="required">*</span></label>
                <textarea
                  id="shortDescription"
                  formControlName="shortDescription"
                  rows="2"
                  placeholder="Kort beskrivning av behandlingstypen..."
                ></textarea>
                @if (shortDescription?.invalid && shortDescription?.touched) {
                  <span class="error">Kort beskrivning krävs</span>
                }
              </div>
            </div>
            <div class="form-row">
              <div class="form-group full-width">
                <label for="publicDescription">Publik beskrivning</label>
                <textarea
                  id="publicDescription"
                  formControlName="publicDescription"
                  rows="3"
                  placeholder="Beskrivning som syns för kunder (valfritt)..."
                ></textarea>
              </div>
            </div>
          </div>

          <!-- Tid -->
          <div class="section">
            <h2 class="section-title">Tid</h2>
            <div class="form-row">
              <div class="form-group">
                <label for="durationMinutes">Duration (min) <span class="required">*</span></label>
                <input
                  id="durationMinutes"
                  type="number"
                  formControlName="durationMinutes"
                  min="1"
                />
                @if (durationMinutes?.invalid && durationMinutes?.touched) {
                  <span class="error">Duration krävs</span>
                }
              </div>
              <div class="form-group">
                <label for="bufferBeforeMinutes">Paus före (min)</label>
                <input
                  id="bufferBeforeMinutes"
                  type="number"
                  formControlName="bufferBeforeMinutes"
                  min="0"
                />
              </div>
              <div class="form-group">
                <label for="bufferAfterMinutes">Paus efter (min)</label>
                <input
                  id="bufferAfterMinutes"
                  type="number"
                  formControlName="bufferAfterMinutes"
                  min="0"
                />
              </div>
            </div>
            <div class="form-row">
              <div class="form-group">
                <label for="minNoticeHours">Minsta anmälan (timmar)</label>
                <input
                  id="minNoticeHours"
                  type="number"
                  formControlName="minNoticeHours"
                  min="0"
                />
              </div>
              <div class="form-group">
                <label for="maxAdvanceDays">Max i förväg (dagar)</label>
                <input
                  id="maxAdvanceDays"
                  type="number"
                  formControlName="maxAdvanceDays"
                  min="0"
                />
              </div>
              <div class="form-group">
                <label for="followUpIntervalDays">Uppföljningsintervall (dagar)</label>
                <input
                  id="followUpIntervalDays"
                  type="number"
                  formControlName="followUpIntervalDays"
                  min="0"
                  placeholder="Tomt = inget krav"
                />
              </div>
            </div>
          </div>

          <!-- Pris -->
          <div class="section">
            <h2 class="section-title">Pris</h2>
            <div class="form-row">
              <div class="form-group">
                <label for="priceExcludingVat">Pris exkl. moms <span class="required">*</span></label>
                <input
                  id="priceExcludingVat"
                  type="number"
                  formControlName="priceExcludingVat"
                  min="0"
                  step="0.01"
                />
                @if (priceExcludingVat?.invalid && priceExcludingVat?.touched) {
                  <span class="error">Pris krävs</span>
                }
              </div>
              <div class="form-group">
                <label for="vatRate">Momsats (%) <span class="required">*</span></label>
                <input
                  id="vatRate"
                  type="number"
                  formControlName="vatRate"
                  min="0"
                  max="100"
                  step="0.01"
                />
                @if (vatRate?.invalid && vatRate?.touched) {
                  <span class="error">Momsats krävs</span>
                }
              </div>
              <div class="form-group">
                <label for="colour">Färg</label>
                <div class="colour-wrapper">
                  <input
                    id="colour"
                    type="color"
                    formControlName="colour"
                    class="colour-input"
                  />
                  <span class="colour-value">{{ colourValue() || 'Ingen färg' }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Inställningar -->
          <div class="section">
            <h2 class="section-title">Inställningar</h2>
            <div class="form-row">
              <div class="form-group">
                <label for="bookableOnline" class="checkbox-label">
                  <input
                    id="bookableOnline"
                    type="checkbox"
                    formControlName="bookableOnline"
                  />
                  Bokningsbar online
                </label>
              </div>
              <div class="form-group">
                <label for="requiresApproval" class="checkbox-label">
                  <input
                    id="requiresApproval"
                    type="checkbox"
                    formControlName="requiresApproval"
                  />
                  Kräver godkännande
                </label>
              </div>
            </div>
            <div class="form-row">
              <div class="form-group full-width">
                <label for="allowedLocationTypes">Tillåtna platstyper</label>
                <textarea
                  id="allowedLocationTypes"
                  formControlName="allowedLocationTypes"
                  rows="2"
                  placeholder="T.ex. Praktikrum,Kliniken,FörHem (kommaseparerat)"
                ></textarea>
              </div>
            </div>
          </div>

          <!-- Journalmall -->
          <div class="section">
            <h2 class="section-title">Journalmall</h2>
            <div class="form-row">
              <div class="form-group full-width">
                <p class="section-hint">Konfigurera en mall för hur journaler ska se ut vid denna behandlingstyp.</p>
                <button
                  type="button"
                  (click)="openTemplateBuilder()"
                  class="btn-secondary"
                >
                  {{ hasTemplate() ? 'Konfigurera mall' : 'Skapa mall' }}
                </button>
                @if (hasTemplate()) {
                  <div class="template-info">
                    <span>Mall konfigurerad ({{ templateSectionCount() }} sektioner)</span>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>

        <div class="form-actions">
          <a routerLink="/treatment-types" class="btn-cancel">Avbryt</a>
          <button
            type="submit"
            [disabled]="treatmentForm.invalid || loading()"
            class="btn-primary"
          >
            {{ loading() ? 'Sparar...' : (isEdit ? 'Uppdatera' : 'Skapa behandlingstyp') }}
          </button>
        </div>
      </form>
    </div>

    @if (showTemplateBuilder()) {
      <div class="modal-overlay" (click)="closeTemplateBuilder()">
        <div class="modal-content" (click)="$event.stopPropagation()">
          <app-template-builder
            [initialTemplate]="templateJson()"
            (templateChange)="onTemplateChange($event)"
            (close)="closeTemplateBuilder()"
          ></app-template-builder>
        </div>
      </div>
    }
  `,
  styles: [`
    .form-container, .section { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-md); padding: 1.25rem; margin-bottom: 1rem; }
    .section-title { margin: 0 0 1rem; color: var(--color-primary-text); }
    .form-row { display: grid; gap: 0.75rem; margin-bottom: 0.75rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.3rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group input, .form-group textarea, .form-group select {
      width: 100%; min-height: var(--tap-min); padding: 0.55rem 0.75rem;
      border: 1px solid var(--color-border-strong); border-radius: var(--radius-sm); background: var(--color-surface);
    }
    .error { color: var(--color-danger); font-size: 0.8125rem; }
    .checkbox-label { display: flex; align-items: center; gap: 0.5rem; }
    .modal-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(36,48,24,.45); display: grid; place-items: center; padding: 1rem; }
    .modal, .modal-content { width: min(900px, 100%); max-height: 90vh; overflow: auto; background: var(--color-surface); border-radius: var(--radius-lg); }
    @media (min-width: 768px) { .form-row { grid-template-columns: 1fr 1fr; } .form-row.three-col { grid-template-columns: 1fr 1fr 1fr; } }
  `]
})
export class TreatmentTypeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private toast = inject(ToastService);
  error = signal('');

  treatmentForm!: FormGroup;
  loading = signal(false);
  isEdit = false;
  treatmentId = signal<string>('');

  showTemplateBuilder = signal(false);
  templateJson = signal('');
  hasTemplate = signal(false);
  templateSectionCount = signal(0);

  colourValue = signal('');

  get name() { return this.treatmentForm.get('name'); }
  get shortDescription() { return this.treatmentForm.get('shortDescription'); }
  get durationMinutes() { return this.treatmentForm.get('durationMinutes'); }
  get priceExcludingVat() { return this.treatmentForm.get('priceExcludingVat'); }
  get vatRate() { return this.treatmentForm.get('vatRate'); }

  ngOnInit(): void {
    this.initForm();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.treatmentId.set(id);
      this.loadTreatmentType(id);
    }
  }

  initForm(): void {
    this.treatmentForm = this.fb.group({
      name: ['', Validators.required],
      slug: [''],
      shortDescription: ['', Validators.required],
      publicDescription: [''],
      durationMinutes: [30, [Validators.required, Validators.min(1)]],
      bufferBeforeMinutes: [0, [Validators.min(0)]],
      bufferAfterMinutes: [0, [Validators.min(0)]],
      colour: [''],
      priceExcludingVat: ['', [Validators.required, Validators.min(0)]],
      vatRate: [25, [Validators.required, Validators.min(0), Validators.max(100)]],
      bookableOnline: [false],
      requiresApproval: [false],
      minNoticeHours: [0, [Validators.min(0)]],
      maxAdvanceDays: [30, [Validators.min(0)]],
      allowedLocationTypes: [''],
      followUpIntervalDays: [null, []],
      journalTemplateJson: ['']
    });

    this.treatmentForm.get('colour')?.valueChanges.subscribe(val => {
      this.colourValue.set(val || '');
    });
  }

  loadTreatmentType(id: string): void {
    this.loading.set(true);
    this.http.get<TreatmentType>(`${environment.apiUrl}/api/app/treatment-types/${id}`).subscribe({
      next: (type) => {
        this.treatmentForm.patchValue({
          name: type.name,
          slug: type.slug || '',
          shortDescription: type.shortDescription,
          publicDescription: type.publicDescription || '',
          durationMinutes: type.durationMinutes,
          bufferBeforeMinutes: type.bufferBeforeMinutes,
          bufferAfterMinutes: type.bufferAfterMinutes,
          colour: type.colour,
          priceExcludingVat: type.priceExclVat ?? type.priceExcludingVat,
          vatRate: type.vatRate * (type.vatRate <= 1 ? 100 : 1),
          bookableOnline: type.bookableOnline,
          requiresApproval: type.requiresApproval,
          minNoticeHours: type.minNoticeHours,
          maxAdvanceDays: type.maxAdvanceDays,
          allowedLocationTypes: type.allowedLocationTypes || '',
          followUpIntervalDays: type.followUpIntervalDays,
          journalTemplateJson: type.journalTemplateJson || ''
        });
        this.colourValue.set(type.colour || '');

        if (type.journalTemplateJson) {
          try {
            const template = JSON.parse(type.journalTemplateJson);
            this.templateJson.set(JSON.stringify(template, null, 2));
            this.hasTemplate.set(true);
            if (template.sections && Array.isArray(template.sections)) {
              this.templateSectionCount.set(template.sections.length);
            }
          } catch {
            this.templateJson.set('');
            this.hasTemplate.set(false);
          }
        }

        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda behandlingstypen.');
        this.loading.set(false);
      }
    });
  }

  onSubmit(): void {
    if (this.treatmentForm.invalid) {
      Object.keys(this.treatmentForm.controls).forEach(key => {
        const control = this.treatmentForm.get(key);
        if (control) {
          control.markAsTouched();
        }
      });
      return;
    }

    this.loading.set(true);

    const formValue = this.treatmentForm.value;
    const payload = {
      name: formValue.name,
      slug: formValue.slug || null,
      shortDescription: formValue.shortDescription,
      publicDescription: formValue.publicDescription,
      durationMinutes: formValue.durationMinutes,
      bufferBeforeMinutes: formValue.bufferBeforeMinutes,
      bufferAfterMinutes: formValue.bufferAfterMinutes,
      colour: formValue.colour || null,
      priceExclVat: formValue.priceExcludingVat,
      vatRate: Number(formValue.vatRate) > 1 ? Number(formValue.vatRate) / 100 : formValue.vatRate,
      bookableOnline: formValue.bookableOnline,
      requiresApproval: formValue.requiresApproval,
      minNoticeHours: formValue.minNoticeHours,
      maxAdvanceDays: formValue.maxAdvanceDays,
      allowedLocationTypes: formValue.allowedLocationTypes,
      followUpIntervalDays: formValue.followUpIntervalDays === '' ? null : formValue.followUpIntervalDays,
      journalTemplateJson: this.templateJson() || null
    };

    const request = this.isEdit
      ? this.http.put(`${environment.apiUrl}/api/app/treatment-types/${this.treatmentId()}`, payload)
      : this.http.post(`${environment.apiUrl}/api/app/treatment-types`, payload);

    request.subscribe({
      next: () => {
        this.toast.success(this.isEdit ? 'Behandlingstypen sparades.' : 'Behandlingstypen skapades.');
        this.router.navigate(['/treatment-types']);
      },
      error: (err) => {
        const message = apiErrorMessage(err) || 'Kunde inte spara behandlingstypen.';
        this.error.set(message);
        this.toast.error(message);
        this.loading.set(false);
      }
    });
  }

  openTemplateBuilder(): void {
    this.showTemplateBuilder.set(true);
  }

  closeTemplateBuilder(): void {
    this.showTemplateBuilder.set(false);
  }

  onTemplateChange(template: string): void {
    this.templateJson.set(template);
    this.hasTemplate.set(template.trim() !== '');
    try {
      const parsed = JSON.parse(template);
      if (parsed.sections && Array.isArray(parsed.sections)) {
        this.templateSectionCount.set(parsed.sections.length);
      }
    } catch {
      this.templateSectionCount.set(0);
    }
  }
}

function apiErrorMessage(err: unknown): string {
  const body = (err as { error?: unknown })?.error;
  if (typeof body === 'string' && body.trim()) return body;
  if (body && typeof body === 'object') {
    const obj = body as { errors?: Record<string, string[] | string>; detail?: string; title?: string; message?: string };
    if (obj.errors) {
      const first = Object.values(obj.errors).flat()[0];
      if (typeof first === 'string' && first.trim()) return first;
    }
    if (typeof obj.detail === 'string' && obj.detail.trim()) return obj.detail;
    if (typeof obj.message === 'string' && obj.message.trim()) return obj.message;
    if (typeof obj.title === 'string' && obj.title.trim()) return obj.title;
  }
  return '';
}
