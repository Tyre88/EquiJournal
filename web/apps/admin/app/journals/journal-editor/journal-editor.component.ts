import { Component, OnInit, DestroyRef, OnChanges, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../../environments/environment';
import { ConfirmService, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { BodymapComponent, BodyMapMarker } from '../bodymap/bodymap.component';
import { AnatomyMapComponent } from '../anatomy-map/anatomy-map.component';
import {
  AnatomyAnnotation,
  AnatomyStroke,
  DEFAULT_ANATOMY_PRESET,
  DEFAULT_FINDING_OPTIONS,
  parseAnatomyMapValue
} from '../anatomy-map/anatomy-map.types';
import { DraftStoreService } from '../../draft-store.service';
import { AttachmentQueueService } from '../../attachment-queue.service';
import { SyncStatusService } from '../../sync-status.service';
const API_URL = environment.apiUrl;

export interface TemplateField {
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'number' | 'select' | 'multiselect' | 'checkbox' | 'date' | 'bodymap' | 'anatomy-map';
  options?: string[];
  min?: number;
  max?: number;
  required?: boolean;
  preset?: string;
  customImageKey?: string | null;
  findingOptions?: string[];
}

export interface TemplateSection {
  key: string;
  label: string;
  type: TemplateField['type'];
  options?: string[];
  min?: number;
  max?: number;
  required?: boolean;
  preset?: string;
  customImageKey?: string | null;
  findingOptions?: string[];
}

export interface JournalTemplate {
  version?: number;
  sections: TemplateSection[];
}

interface HorseItem {
  id: string;
  name: string;
  ownerName: string;
}

interface TreatmentTypeItem {
  id: string;
  name: string;
  journalTemplate?: JournalTemplate;
}

interface AttachmentItem {
  id: string;
  name: string;
  mimeType: string;
  size: number;
  uploadedAt: string;
  downloadUrl?: string;
}

type SaveStatus = 'idle' | 'saving' | 'saved' | 'error';

@Component({
  selector: 'app-journal-editor',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, BodymapComponent, AnatomyMapComponent, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header [title]="isNew() ? 'Ny journal' : 'Redigera journal'" backHref="/journals">
        <span class="muted">{{ saveStatusText() }} {{ syncStatus() === 'offline' ? '· offline' : syncStatus() === 'pending' ? '· synkar' : '' }}</span>
      </ej-page-header>
      <main>
        @if (loading()) {
          <div class="loading">Laddar...</div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          <!-- Header section -->
          <div class="card editor-section">
            <h2>Information</h2>

            <div class="form-row">
              <div class="form-group">
                <label for="horse">Häst <span class="required">*</span></label>
                <div class="search-select">
                  <input
                    id="horse"
                    type="text"
                    placeholder="Sök häst..."
                    [value]="selectedHorseText()"
                    (input)="onHorseSearch($event)"
                    (focus)="onHorseFocus()"
                    autocomplete="off"
                  />
                  @if (showHorseDropdown() && horseSearchResults().length > 0) {
                    <ul class="search-dropdown">
                      @for (horse of horseSearchResults(); track horse.id) {
                        <li
                          class="search-option"
                          [class.active]="selectedHorse()?.id === horse.id"
                          (click)="selectHorse(horse)"
                          (keydown.enter)="selectHorse(horse)"
                          tabindex="0"
                        >
                          <span class="horse-name">{{ horse.name }}</span>
                          <span class="horse-owner">{{ horse.ownerName }}</span>
                        </li>
                      }
                    </ul>
                  }
                  @if (showHorseDropdown() && horseSearchQuery() && horseSearchResults().length === 0 && !horseSearchLoading()) {
                    <ul class="search-dropdown">
                      <li class="search-empty">Inga hästar hittades</li>
                    </ul>
                  }
                  @if (horseSearchLoading()) {
                    <span class="search-loading">Söker...</span>
                  }
                </div>
                <div class="selected-horse" formArrayName="horseId">
                  @if (selectedHorse()) {
                    <span class="selected-tag">
                      {{ selectedHorse()!.name }} ({{ selectedHorse()!.ownerName }})
                      <button type="button" class="tag-remove" (click)="clearHorse()">×</button>
                    </span>
                  }
                </div>
                @if (form.get('horseId')?.invalid && form.get('horseId')?.touched) {
                  <span class="error">Val av häst krävs</span>
                }
              </div>
            </div>

            <div class="form-row">
              <div class="form-group half">
                <label for="treatmentType">Behandlingstyp <span class="required">*</span></label>
                <select
                  id="treatmentType"
                  formControlName="treatmentTypeId"
                >
                  <option value="">— Välj behandlingstyp —</option>
                  @for (tt of treatmentTypes(); track tt.id) {
                    <option [value]="tt.id">{{ tt.name }}</option>
                  }
                </select>
                @if (form.get('treatmentTypeId')?.invalid && form.get('treatmentTypeId')?.touched) {
                  <span class="error">Behandlingstyp krävs</span>
                }
              </div>
            </div>

            <div class="form-row">
              <div class="form-group half">
                <label for="performedAt">Utförd den <span class="required">*</span></label>
                <input
                  id="performedAt"
                  type="datetime-local"
                  formControlName="performedAt"
                />
              </div>
            </div>
          </div>

          <!-- Base fields (required) -->
          <div class="card editor-section">
            <h2>Basfält <span class="muted">(obligatoriska)</span></h2>

            <div class="form-group full">
              <label for="anamnes">Anamnes <span class="required">*</span></label>
              <textarea
                id="anamnes"
                formControlName="anamnes"
                rows="4"
                placeholder="Anamnesebeskrivning..."
                (blur)="onFieldBlur()"
              ></textarea>
              @if (anamnes?.invalid && anamnes?.touched) {
                <span class="error">Anamnes krävs</span>
              }
            </div>

            <div class="form-group full">
              <label for="statusKlinisk">Status Klinisk <span class="required">*</span></label>
              <textarea
                id="statusKlinisk"
                formControlName="statusKlinisk"
                rows="4"
                placeholder="Klinisk bedömning..."
                (blur)="onFieldBlur()"
              ></textarea>
              @if (statusKlinisk?.invalid && statusKlinisk?.touched) {
                <span class="error">Status Klinisk krävs</span>
              }
            </div>

            <div class="form-group full">
              <label for="atgarder">Undersökning/behandling och motivering <span class="required">*</span></label>
              <textarea
                id="atgarder"
                formControlName="atgarder"
                rows="4"
                placeholder="Undersökning, behandling och motivering..."
              ></textarea>
              @if (atgarder?.invalid && atgarder?.touched) {
                <span class="error">Undersökning/behandling och motivering krävs</span>
              }
            </div>
          </div>

          <!-- Base fields (optional) -->
          <div class="card editor-section">
            <h2>Övriga fält</h2>

            <div class="form-group full">
              <label for="prognosOchPlan">Prognos och plan / hemgångsråd</label>
              <textarea
                id="prognosOchPlan"
                formControlName="prognosOchPlan"
                rows="3"
                placeholder="Prognos, plan och hemgångsråd..."
              ></textarea>
            </div>
          </div>

          <!-- Template fields -->
          @if (templateFields().length > 0) {
            <div class="card editor-section">
              <h2>Shablonfält</h2>
              @for (section of templateFields(); track section.key) {
                <div class="template-section-group">
                  <h3>{{ section.label }}</h3>
                  @for (field of section.fields; track field.key) {
                    <div class="form-group full">
                      <label [for]="'field-' + field.key">{{ field.label }}
                        @if (field.type === 'checkbox') {
                          <input type="checkbox" [id]="'chk-' + field.key" [formControlName]="'tpl_' + field.key" />
                        }
                      </label>

                      @if (field.type === 'textarea') {
                        <textarea
                          [id]="'field-' + field.key"
                          [formControlName]="'tpl_' + field.key"
                          rows="3"
                          placeholder="{{ field.label }}..."
                        ></textarea>
                      }
                      @if (field.type === 'select') {
                        <select [id]="'field-' + field.key" [formControlName]="'tpl_' + field.key">
                          <option value="">— Välj —</option>
                          @if (field.options) {
                            @for (opt of field.options; track opt) {
                              <option [value]="opt">{{ opt }}</option>
                            }
                          }
                        </select>
                      }
                      @if (field.type === 'number') {
                        <input
                          [id]="'field-' + field.key"
                          type="number"
                          [formControlName]="'tpl_' + field.key"
                          [min]="field.min"
                          [max]="field.max"
                          placeholder="{{ field.label }}..."
                        />
                      }
                      @if (field.type === 'text') {
                        <input
                          [id]="'field-' + field.key"
                          type="text"
                          [formControlName]="'tpl_' + field.key"
                          placeholder="{{ field.label }}..."
                        />
                      }
                      @if (field.type === 'date') {
                        <input
                          [id]="'field-' + field.key"
                          type="date"
                          [formControlName]="'tpl_' + field.key"
                        />
                      }
                      @if (field.type === 'multiselect') {
                        <div class="multiselect-group">
                          @for (opt of field.options ?? []; track opt) {
                            <label class="multiselect-item">
                              <input
                                type="checkbox"
                                [value]="opt"
                                (change)="onMultiSelectChange($event, field.key)"
                              />
                              {{ opt }}
                            </label>
                          }
                        </div>
                      }
                      @if (field.type === 'bodymap') {
                        <div class="bodymap-field">
                          <app-bodymap
                            [markers]="bodyMapMarkers(field.key)"
                            (add)="onBodyMapAdd(field.key, $event)"
                            (labelChange)="onBodyMapLabel(field.key, $event)"
                            (noteChange)="onBodyMapNote(field.key, $event)"
                            (remove)="onBodyMapRemove(field.key, $event)"
                          />
                        </div>
                      }
                      @if (field.type === 'anatomy-map') {
                        <div class="anatomy-field">
                          <app-anatomy-map
                            [presetId]="field.preset || 'horse-muscles-standard'"
                            [customImageUrl]="anatomyImageUrls()[field.key] || null"
                            [findingOptions]="field.findingOptions ?? []"
                            [annotations]="anatomyAnnotations(field.key)"
                            [strokes]="anatomyStrokes(field.key)"
                            (annotationChange)="onAnatomyChange(field, $event)"
                            (strokesChange)="onAnatomyStrokesChange(field, $event)"
                          />
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }

          <!-- Attachments -->
          <div class="card editor-section">
            <h2>Bilagor</h2>

            <div class="upload-zone"
                 (dragover)="onDragOver($event)"
                 (dragleave)="onDragLeave($event)"
                 (drop)="onDrop($event)"
                 [class.dragover]="isDragOver()">
              <input
                type="file"
                id="fileUpload"
                multiple
                accept="image/jpeg,image/png,image/heic,application/pdf,video/mp4"
                (change)="onFileSelect($event)"
                class="file-input"
              />
              <div class="upload-zone-content">
                <p>Drag och släpp filer här, eller <label for="fileUpload" class="upload-link">klicka för att välja</label></p>
                <p class="upload-hint">JPEG, PNG, HEIC, PDF, MP4 (max 50 MB per fil)</p>
              </div>
            </div>

            @if (pendingFiles().length > 0) {
              <div class="pending-files">
                @for (f of pendingFiles(); track f.name + f.lastModified) {
                  <div class="pending-file">
                    <span>{{ f.name }} ({{ formatSize(f.size) }})</span>
                    <button type="button" class="btn-remove-file" (click)="removePendingFile(f)">×</button>
                  </div>
                }
              </div>
            }

            @if (attachments().length > 0) {
              <ul class="attached-list">
                @for (att of attachments(); track att.id) {
                  <li class="attached-item">
                    <a [href]="getAttachmentUrl(att)"
                       target="_blank"
                       rel="noopener noreferrer">
                      {{ att.name }}
                    </a>
                    <span class="attached-size">{{ formatSize(att.size) }}</span>
                    <button type="button" class="btn-remove-attached" (click)="removeAttachment(att.id)">×</button>
                  </li>
                }
              </ul>
            }

            @if (uploading()) {
              <div class="upload-status">Laddar upp...</div>
            }
          </div>

          <div class="actions-bar">
            <span class="muted">{{ saveStatusText() }}</span>
            <button type="submit" class="btn-primary" [disabled]="saving() || form.invalid">
              {{ saving() ? 'Sparar…' : 'Spara utkast' }}
            </button>
            @if (journalId()) {
              <button type="button" class="btn-secondary" (click)="signJournal()" [disabled]="saving() || saveStatus() === 'saving'">
                Signera
              </button>
            }
          </div>
        </form>
      </main>
    </div>
  `,
  styles: [`
    .form-row { display: flex; flex-wrap: wrap; gap: 1rem; }
    .form-group { flex: 1; min-width: 220px; margin-bottom: 1rem; }
    .form-group.full { min-width: 100%; }
    .form-group label { display: block; font-weight: 600; font-size: 0.875rem; margin-bottom: 0.35rem; }
    .form-group input, .form-group select, .form-group textarea { width: 100%; }
    .error { color: var(--color-danger); font-size: 0.8125rem; }
    .multiselect-group { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .anatomy-field { min-height: 300px; }
    .pending-file, .attached-item { display: flex; justify-content: space-between; gap: 0.75rem; padding: 0.5rem 0; }
    .btn-remove-file, .btn-remove-attached, .tag-remove { border: 0; background: none; color: var(--color-danger); cursor: pointer; min-height: 32px; }
  `]
})
export class JournalEditorComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);
  private drafts = inject(DraftStoreService);
  private attachmentQueue = inject(AttachmentQueueService);
  private syncStatusSvc = inject(SyncStatusService);
  private toast = inject(ToastService);
  private confirmDlg = inject(ConfirmService);
  localDraftId = crypto.randomUUID();
  syncStatus = signal<'pending' | 'synced' | 'error' | 'offline'>('synced');

  journalId = signal<string | null>(null);
  isNew = computed(() => {
    const id = this.journalId();
    return !id || id === 'new';
  });

  form: FormGroup;
  loading = signal(true);
  saving = signal(false);
  uploading = signal(false);

  horses = signal<HorseItem[]>([]);
  horseSearchQuery = signal('');
  horseSearchResults = signal<HorseItem[]>([]);
  horseSearchLoading = signal(false);
  showHorseDropdown = signal(false);
  selectedHorse = signal<HorseItem | null>(null);
  selectedHorseText = computed(() => this.selectedHorse() ? `${this.selectedHorse()!.name} (${this.selectedHorse()!.ownerName})` : '');

  treatmentTypes = signal<TreatmentTypeItem[]>([]);
  templateFields = signal<{ key: string; label: string; fields: TemplateField[] }[]>([]);

  pendingFiles = signal<File[]>([]);
  attachments = signal<AttachmentItem[]>([]);
  isDragOver = signal(false);

  saveStatus = signal<SaveStatus>('idle');
  saveStatusText = computed(() => {
    const s = this.saveStatus();
    switch (s) {
      case 'saving': return 'Sparar...';
      case 'saved': return 'Sparad';
      case 'error': return 'FEL';
      default: return '';
    }
  });

  bodymapOpen = signal(false);
  bodymapFieldKey = signal('');
  bodymapFieldLabel = computed(() => {
    const key = this.bodymapFieldKey();
    const sections = this.templateFields();
    for (const sec of sections) {
      for (const f of sec.fields) {
        if (f.key === key) return f.label;
      }
    }
    return '';
  });

  showSignDialog = signal(false);
  anatomyImageUrls = signal<Record<string, string>>({});

  private autosaveTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.form = this.fb.group({
      horseId: ['', Validators.required],
      treatmentTypeId: ['', Validators.required],
      performedAt: ['', Validators.required],
      anamnes: ['', Validators.required],
      statusKlinisk: ['', Validators.required],
      atgarder: ['', Validators.required],
      diagnos: [''],
      differentialdiagnoser: [''],
      prognosOchPlan: ['']
    });
  }

  ngOnInit(): void {
    this.journalId.set(this.route.snapshot.paramMap.get('id'));

    this.loadTreatmentTypes();
    this.loadHorses();

    if (!this.isNew()) {
      const id = this.route.snapshot.paramMap.get('id');
      if (id) {
        this.loadJournal(id);
      }
    } else {
      this.form.get('performedAt')?.setValue(this.getDefaultDateTime());
      this.loading.set(false);
    }

    this.form.get('treatmentTypeId')?.valueChanges.subscribe(id => {
      if (id) this.loadTemplateForTreatment(id);
    });
    this.form.valueChanges.subscribe(() => this.persistOffline());
    this.startAutosave();
    this.restoreOffline();
    window.addEventListener('online', () => this.flushOffline());
    document.addEventListener('click', this.onDocumentClick);
  }

  ngOnDestroy(): void {
    if (this.autosaveTimer) {
      clearInterval(this.autosaveTimer);
    }
    document.removeEventListener('click', this.onDocumentClick);
  }

  private onDocumentClick = (event: Event): void => {
    if (this.showHorseDropdown() && !(event.target instanceof HTMLElement)) return;
    if (event.target instanceof HTMLElement && event.target.closest('.search-select')) return;
    this.showHorseDropdown.set(false);
  };

  private loadTreatmentTypes(): void {
    this.http.get<any>(`${API_URL}/api/app/treatment-types/active`).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (response: any) => {
        const types = response.results ?? response;
        this.treatmentTypes.set(types);
      },
      error: () => {}
    });
  }

  private loadHorses(): void {
    this.http.get<any>(`${API_URL}/api/app/horses`).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (response: any) => {
        this.horses.set(response.results ?? response);
      },
      error: () => {}
    });
  }

  private loadJournal(id: string): void {
    this.http.get<any>(`${API_URL}/api/app/journals/${id}`).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (response: any) => {
        const j = response;

        this.form.patchValue({
          horseId: j.horseId,
          treatmentTypeId: j.treatmentTypeId,
          performedAt: j.performedAtLocal || this.formatToLocalDateTime(j.performedAt),
          anamnes: j.anamnes || '',
          statusKlinisk: j.statusKlinisk || '',
          atgarder: j.atgarder || '',
          diagnos: j.diagnos || '',
          differentialdiagnoser: j.differentialdiagnoser || '',
          prognosOchPlan: j.prognosOchPlan || ''
        });

        if (j.horseId) {
          const horse = this.horses().find(h => h.id === j.horseId);
          if (horse) {
            this.selectedHorse.set(horse);
            this.horseSearchQuery.set(`${horse.name} (${horse.ownerName})`);
          }
        }

        if (j.status === 'Signed') {
          this.router.navigate(['/journals', id]);
          return;
        }
        const templateData = typeof j.templateDataJson === 'string'
          ? JSON.parse(j.templateDataJson || '{}')
          : (j.templateData || {});
        if (j.treatmentTypeId) {
          this.loadTemplateForTreatment(j.treatmentTypeId, templateData);
        } else if (templateData && Object.keys(templateData).length) {
          this.patchTemplateFields(templateData);
        }

        if ((j as any).attachments) {
          this.attachments.set((j as any).attachments);
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  private buildTemplateFields(journal: any): void {
    const template = journal.treatmentType?.journalTemplate ?? journal.treatmentTypeData?.journalTemplate;
    if (!template?.sections) return;

    const fields: { key: string; label: string; fields: TemplateField[] }[] = [];
    const templateData = journal.templateData || {};

    for (const section of template.sections) {
      const tplFields: TemplateField[] = [];

      const sectionType = String(section.type ?? '').toLowerCase();
      const isAnatomy = sectionType === 'anatomy-map' || sectionType === 'anatomymap'
        || section.preset === 'horse-muscles-standard'
        || Array.isArray(section.findingOptions);
      if (sectionType === 'bodymap' && !isAnatomy) {
        tplFields.push({
          key: section.key,
          label: section.label,
          type: 'bodymap'
        });
      } else if (isAnatomy) {
        const anatomyField: TemplateField = {
          key: section.key,
          label: section.label,
          type: 'anatomy-map',
          preset: section.preset || DEFAULT_ANATOMY_PRESET,
          customImageKey: section.customImageKey ?? null,
          findingOptions: Array.isArray(section.findingOptions) && section.findingOptions.length
            ? section.findingOptions
            : DEFAULT_FINDING_OPTIONS
        };
        tplFields.push(anatomyField);
        const value = parseAnatomyMapValue(templateData[section.key], anatomyField.preset);
        if (!value.customImageKey && anatomyField.customImageKey) {
          value.customImageKey = anatomyField.customImageKey;
        }
        if (!this.form.contains('tpl_' + section.key)) {
          this.form.addControl('tpl_' + section.key, this.fb.control(value));
        } else {
          this.form.get('tpl_' + section.key)?.setValue(value);
        }
        const imageKey = value.customImageKey || anatomyField.customImageKey;
        if (imageKey) this.resolveAnatomyImage(section.key, imageKey);
      } else {
        tplFields.push({
          key: section.key,
          label: section.label,
          type: section.type as TemplateField['type'],
          options: section.options,
          min: section.min,
          max: section.max,
          required: section.required
        });
      }

      fields.push({ key: section.key, label: section.label, fields: tplFields });

      for (const f of tplFields) {
        if (f.type === 'bodymap' || f.type === 'anatomy-map') continue;
        if (!this.form.contains('tpl_' + f.key)) {
          if (f.type === 'multiselect') {
            this.form.addControl('tpl_' + f.key, this.fb.control(templateData[section.key] || []));
          } else {
            this.form.addControl('tpl_' + f.key, this.fb.control(templateData[section.key] ?? ''));
          }
        }
      }
    }

    this.templateFields.set(fields);
  }

  private patchTemplateFields(templateData: Record<string, any>): void {
    if (!templateData) return;
    const patch: Record<string, any> = {};

    for (const [key, value] of Object.entries(templateData)) {
      patch['tpl_' + key] = value;
    }

    if (Object.keys(patch).length > 0) {
      this.form.patchValue(patch);
    }
  }

  private getBodyMapValue(key: string): string {
    const ctrl = this.form.get('tpl_' + key);
    return ctrl?.value ?? '';
  }

  openBodymap(key: string): void {
    this.bodymapFieldKey.set(key);
    this.bodymapOpen.set(true);
  }

  closeBodymap(): void {
    this.bodymapOpen.set(false);
    this.bodymapFieldKey.set('');
  }

  bodyMapMarkers(key: string): BodyMapMarker[] {
    const raw = this.form.get('tpl_' + key)?.value;
    if (!raw) return [];
    try {
      const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
      return parsed.markers ?? [];
    } catch {
      return [];
    }
  }

  private setBodyMapMarkers(key: string, markers: BodyMapMarker[]): void {
    let ctrl = this.form.get('tpl_' + key);
    if (!ctrl) {
      this.form.addControl('tpl_' + key, this.fb.control(''));
      ctrl = this.form.get('tpl_' + key);
    }
    ctrl?.setValue(JSON.stringify({ markers }));
  }

  onBodyMapAdd(key: string, ev: { x: number; y: number; side: 'L' | 'R' }): void {
    const markers = [...this.bodyMapMarkers(key), {
      id: crypto.randomUUID(),
      x: ev.x,
      y: ev.y,
      side: ev.side,
      label: `${this.bodyMapMarkers(key).length + 1}`,
      note: ''
    }];
    this.setBodyMapMarkers(key, markers);
  }

  onBodyMapLabel(key: string, ev: { id: string; label: string }): void {
    this.setBodyMapMarkers(key, this.bodyMapMarkers(key).map(m => m.id === ev.id ? { ...m, label: ev.label } : m));
  }

  onBodyMapNote(key: string, ev: { id: string; note: string }): void {
    this.setBodyMapMarkers(key, this.bodyMapMarkers(key).map(m => m.id === ev.id ? { ...m, note: ev.note } : m));
  }

  onBodyMapRemove(key: string, id: string): void {
    this.setBodyMapMarkers(key, this.bodyMapMarkers(key).filter(m => m.id !== id));
  }

  anatomyAnnotations(key: string): AnatomyAnnotation[] {
    return parseAnatomyMapValue(this.form.get('tpl_' + key)?.value).annotations;
  }

  anatomyStrokes(key: string): AnatomyStroke[] {
    return parseAnatomyMapValue(this.form.get('tpl_' + key)?.value).strokes;
  }

  onAnatomyChange(field: TemplateField, annotations: AnatomyAnnotation[]): void {
    const current = parseAnatomyMapValue(this.form.get('tpl_' + field.key)?.value, field.preset);
    this.setAnatomyValue(field, { ...current, annotations });
  }

  onAnatomyStrokesChange(field: TemplateField, strokes: AnatomyStroke[]): void {
    const current = parseAnatomyMapValue(this.form.get('tpl_' + field.key)?.value, field.preset);
    this.setAnatomyValue(field, { ...current, strokes });
  }

  private setAnatomyValue(field: TemplateField, current: ReturnType<typeof parseAnatomyMapValue>): void {
    const value = {
      preset: field.preset || current.preset || DEFAULT_ANATOMY_PRESET,
      customImageKey: field.customImageKey ?? current.customImageKey ?? null,
      annotations: current.annotations,
      strokes: current.strokes
    };
    if (!this.form.contains('tpl_' + field.key)) {
      this.form.addControl('tpl_' + field.key, this.fb.control(value));
    } else {
      this.form.get('tpl_' + field.key)?.setValue(value);
    }
  }

  private resolveAnatomyImage(fieldKey: string, storageKey: string): void {
    this.http.get<{ url?: string; Url?: string }>(
      `${API_URL}/api/app/settings/anatomy-images/url`,
      { params: { key: storageKey } }
    ).subscribe({
      next: r => {
        const url = r.url ?? r.Url;
        if (url) this.anatomyImageUrls.update(m => ({ ...m, [fieldKey]: url }));
      }
    });
  }

  onMultiSelectChange(event: Event, key: string): void {
    const checkbox = event.target as HTMLInputElement;
    const value = checkbox.value;
    const ctrl = this.form.get('tpl_' + key);
    if (!ctrl || !Array.isArray(ctrl.value)) return;

    const values = [...ctrl.value] as string[];
    if (checkbox.checked) {
      if (!values.includes(value)) values.push(value);
    } else {
      const idx = values.indexOf(value);
      if (idx >= 0) values.splice(idx, 1);
    }
    ctrl.setValue(values);
  }

  // Horse search
  onHorseSearch(event: Event): void {
    const query = (event.target as HTMLInputElement).value;
    this.horseSearchQuery.set(query);
    this.showHorseDropdown.set(true);

    if (query.length < 2) {
      this.horseSearchResults.set([]);
      return;
    }

    this.horseSearchLoading.set(true);
    this.http.post<any>(`${API_URL}/api/app/horses/search`, { query }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (response: any) => {
        this.horseSearchResults.set(response.results ?? response);
        this.horseSearchLoading.set(false);
      },
      error: () => {
        this.horseSearchResults.set([]);
        this.horseSearchLoading.set(false);
      }
    });
  }

  onHorseFocus(): void {
    if (this.horseSearchQuery().length >= 2) {
      this.showHorseDropdown.set(true);
    }
  }

  selectHorse(horse: HorseItem): void {
    this.selectedHorse.set(horse);
    this.form.get('horseId')?.setValue(horse.id);
    this.horseSearchQuery.set('');
    this.showHorseDropdown.set(false);
  }

  clearHorse(): void {
    this.selectedHorse.set(null);
    this.form.get('horseId')?.setValue('');
  }

  // File upload
  onFileSelect(event: Event): void {
    const files = (event.target as HTMLInputElement).files;
    if (!files) return;
    this.addFiles(Array.from(files));
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (files) {
      this.addFiles(Array.from(files));
    }
  }

  private addFiles(files: File[]): void {
    const validFiles = files.filter(f => {
      const maxSize = 50 * 1024 * 1024;
      if (f.size > maxSize) {
        this.toast.error(`Filen ${f.name} är större än 50 MB och kunde inte läggas till.`);
        return false;
      }
      const allowed = ['image/jpeg', 'image/png', 'image/heic', 'application/pdf', 'video/mp4'];
      if (!allowed.includes(f.type)) {
        this.toast.error(`Filtypen ${f.type} stöds inte.`);
        return false;
      }
      return true;
    });

    this.pendingFiles.update(files => [...files, ...validFiles]);
  }

  removePendingFile(file: File): void {
    this.pendingFiles.update(files => files.filter(f => f !== file));
  }

  removeAttachment(id: string): void {
    this.http.delete(`${API_URL}/api/app/journals/${this.journalId()}/attachments/${id}`).subscribe({
      next: () => {
        this.attachments.update(a => a.filter(att => att.id !== id));
      },
      error: () => {
        this.toast.error('Misslyckades med att ta bort bilaga.');
      }
    });
  }

  private uploadFiles(): void {
    if (this.pendingFiles().length === 0) return;

    const id = this.journalId();
    if (!id || id === 'new') return;

    const files = [...this.pendingFiles()];
    this.pendingFiles.set([]);

    if (!navigator.onLine) {
      void Promise.all(files.map(f => this.attachmentQueue.enqueue(id, f))).then(() => {
        this.syncStatus.set('offline');
        this.syncStatusSvc.setSyncing(files.length);
        this.toast.success('Bilagor köade — synkas vid uppkoppling.');
      });
      return;
    }

    this.uploading.set(true);
    const uploadPromises = files.map(file => {
      const formData = new FormData();
      formData.append('file', file);
      return this.http.post<any>(`${API_URL}/api/app/journals/${id}/attachments`, formData).toPromise();
    });

    Promise.all(uploadPromises)
      .then((results) => {
        const newAttachments = results.map(r => ({
          id: r.id,
          name: r.fileName ?? r.name ?? '',
          mimeType: r.mimeType ?? '',
          size: r.size ?? 0,
          uploadedAt: r.uploadedAt ?? new Date().toISOString(),
          downloadUrl: r.downloadUrl ?? ''
        }));
        this.attachments.update(a => [...a, ...newAttachments]);
        this.uploading.set(false);
      })
      .catch(() => {
        this.uploading.set(false);
        this.toast.error('Misslyckades med att ladda upp filer.');
      });
  }

  // Autosave
  private startAutosave(): void {
    this.autosaveTimer = setInterval(() => {
      this.triggerAutosave();
    }, 5000);
  }

  private triggerAutosave(): void {
    if (this.saving()) return;
    const id = this.journalId();
    if (!id || id === 'new') return;
    this.saveJournal(id, false);
  }

  private saveJournal(id: string, notify: boolean): void {
    if (this.saving()) return;

    this.saving.set(true);
    this.saveStatus.set('saving');

    const formValue = this.form.value;

    // Build template_data from dynamic fields
    const templateData: Record<string, any> = {};
    for (const [key, control] of Object.entries(this.form.controls)) {
      if (key.startsWith('tpl_')) {
        const tplKey = key.substring(4);
        templateData[tplKey] = control.value;
      }
    }

    const payload = this.buildPayload();

    this.http.put(`${API_URL}/api/app/journals/${id}`, payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.saveStatus.set('saved');
        setTimeout(() => {
          if (this.saveStatus() === 'saved') {
            this.saveStatus.set('idle');
          }
        }, 3000);
      },
      error: () => {
        this.saving.set(false);
        this.saveStatus.set('error');
        setTimeout(() => {
          if (this.saveStatus() === 'error') {
            this.saveStatus.set('idle');
          }
        }, 3000);
      }
    });
  }

  // Form control accessors
  get anamnes() { return this.form.get('anamnes'); }
  get statusKlinisk() { return this.form.get('statusKlinisk'); }
  get atgarder() { return this.form.get('atgarder'); }

  // Submit
  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const id = this.journalId();
    if (!id || id === 'new') {
      this.createJournal();
      return;
    }

    this.saveJournal(id, true);
  }

  private createJournal(): void {
    this.saving.set(true);

    const formValue = this.form.value;
    const templateData: Record<string, any> = {};
    for (const [key, control] of Object.entries(this.form.controls)) {
      if (key.startsWith('tpl_')) {
        templateData[key.substring(4)] = control.value;
      }
    }

    const payload = this.buildPayload();

    this.http.post<any>(`${API_URL}/api/app/journals`, payload).subscribe({
      next: (response: any) => {
        const newId = response.id;
        this.journalId.set(newId);
        this.saving.set(false);
        this.saveStatus.set('saved');
        setTimeout(() => this.saveStatus.set('idle'), 3000);
        this.uploadFiles();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error('Misslyckades med att skapa journal: ' + (err.error?.message || err.message));
      }
    });
  }

  async signJournal(): Promise<void> {
    const id = this.journalId();
    if (!id) return;
    const ok = await this.confirmDlg.confirm({
      title: 'Signera journal',
      message: 'När du signerar blir journalen låst. Endast tillägg kan göras efteråt.',
      confirmLabel: 'Ja, signera'
    });
    if (!ok) return;
    this.saving.set(true);
    this.http.post(`${API_URL}/api/app/journals/${id}/sign`, {}).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Journalen signerades.');
        this.router.navigate(['/journals', id]);
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Misslyckades med att signera journalen.');
      }
    });
  }

  // Navigation
  goBack(): void {
    this.router.navigate(['/journals']);
  }

  onFieldBlur(): void {
    const id = this.journalId();
    if (id && id !== 'new') this.saveJournal(id, false);
    else this.persistOffline();
  }

  private buildPayload(): Record<string, unknown> {
    const formValue = this.form.value;
    const templateData: Record<string, unknown> = {};
    for (const [key, control] of Object.entries(this.form.controls)) {
      if (key.startsWith('tpl_')) templateData[key.substring(4)] = control.value;
    }
    const performed = formValue.performedAt
      ? new Date(formValue.performedAt).toISOString()
      : new Date().toISOString();
    return {
      horseId: formValue.horseId,
      treatmentTypeId: formValue.treatmentTypeId || null,
      performedAt: performed,
      anamnes: formValue.anamnes ?? '',
      statusKlinisk: formValue.statusKlinisk ?? '',
      atgarder: formValue.atgarder ?? '',
      diagnos: formValue.diagnos || null,
      differentialdiagnoser: formValue.differentialdiagnoser || null,
      prognosOchPlan: formValue.prognosOchPlan || null,
      templateDataJson: JSON.stringify(templateData)
    };
  }

  private loadTemplateForTreatment(id: string, existing?: Record<string, unknown>): void {
    this.http.get<any>(`${API_URL}/api/app/treatment-types/${id}`).subscribe({
      next: type => {
        const raw = type.journalTemplateJson ?? type.JournalTemplateJson;
        if (!raw) return;
        try {
          const template = typeof raw === 'string' ? JSON.parse(raw) : raw;
          this.buildTemplateFields({ treatmentType: { journalTemplate: template }, templateData: existing ?? {} });
          if (existing) this.patchTemplateFields(existing);
        } catch { /* ignore */ }
      }
    });
  }

  private persistOffline(): void {
    const payload = this.buildPayload();
    this.drafts.put({
      localId: this.localDraftId,
      serverId: this.journalId() && this.journalId() !== 'new' ? this.journalId() : null,
      payload,
      updatedAt: new Date().toISOString(),
      syncStatus: navigator.onLine ? 'pending' : 'pending'
    }).then(() => {
      if (!navigator.onLine) this.syncStatus.set('offline');
    });
  }

  private async restoreOffline(): Promise<void> {
    const id = this.journalId();
    const stored = id && id !== 'new'
      ? await this.drafts.getByServerId(id)
      : await this.drafts.get(this.localDraftId);
    if (!stored) return;
    const p = stored.payload as any;
    this.form.patchValue({
      horseId: p.horseId ?? '',
      treatmentTypeId: p.treatmentTypeId ?? '',
      performedAt: p.performedAt ? this.formatToLocalDateTime(p.performedAt) : this.form.value.performedAt,
      anamnes: p.anamnes ?? '',
      statusKlinisk: p.statusKlinisk ?? '',
      atgarder: p.atgarder ?? '',
      diagnos: p.diagnos ?? '',
      differentialdiagnoser: p.differentialdiagnoser ?? '',
      prognosOchPlan: p.prognosOchPlan ?? ''
    });
    this.syncStatus.set(stored.syncStatus === 'synced' ? 'synced' : 'pending');
  }

  private flushOffline(): void {
    const id = this.journalId();
    if (id && id !== 'new') this.saveJournal(id, false);
    this.syncStatus.set('synced');
  }

  // Helpers
  getDefaultDateTime(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  formatToLocalDateTime(dateStr: string): string {
    if (!dateStr) return this.getDefaultDateTime();
    try {
      const d = new Date(dateStr);
      return d.toISOString().slice(0, 16);
    } catch {
      return this.getDefaultDateTime();
    }
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  getAttachmentUrl(att: { id: string; downloadUrl: string }): string {
    if (att.downloadUrl) return att.downloadUrl;
    return `${API_URL}/api/app/journals/${this.journalId()}/attachments/${att.id}/download`;
  }
}

