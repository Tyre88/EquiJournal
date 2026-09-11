import {
  Component,
  input,
  output,
  OnInit,
  inject,
  signal,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ConfirmService, ToastService } from '@equijournal/ui';
import { environment } from '../../../environments/environment';
import { AnatomyMapComponent } from '../../journals/anatomy-map/anatomy-map.component';
import { DEFAULT_FINDING_OPTIONS } from '../../journals/anatomy-map/anatomy-map.types';

const API_URL = environment.apiUrl;
const DEFAULT_FINDINGS = DEFAULT_FINDING_OPTIONS.join(', ');

function generateId(): string {
  return `sec_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
}

export interface TemplateSection {
  id: string;
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'number' | 'select' | 'multiselect' | 'checkbox' | 'date' | 'bodymap' | 'anatomy-map';
  options?: string;
  minValue?: number | null;
  maxValue?: number | null;
  preset?: string;
  findingOptions?: string;
  customImageKey?: string;
}

interface TemplateData {
  sections: TemplateSection[];
}

@Component({
  selector: 'app-template-builder',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AnatomyMapComponent],
  template: `
    <div class="template-builder">
      <header class="builder-header">
        <h2>Journalmall</h2>
        <div class="builder-actions">
          <button type="button" (click)="togglePreview()" class="btn-secondary">
            {{ previewMode() ? 'Dold förhandsvisning' : 'Förhandsvisning' }}
          </button>
          <button type="button" (click)="close.emit()" class="btn-ghost btn-close" aria-label="Stäng">&times;</button>
        </div>
      </header>

      <div class="builder-body">
        <!-- Sections list -->
        <div class="sections-panel">
          <div class="panel-header">
            <h3>Sektioner</h3>
            <button type="button" (click)="addSection()" class="btn-secondary">
              + Lägg till fält
            </button>
          </div>

          @if (sections().length === 0) {
            <div class="empty-hint">
              Klicka på "Lägg till fält" för att skapa en mall.
            </div>
          } @else {
            <div class="sections-list">
              @for (section of sections(); track section.id; let index = $index) {
                <div class="section-card" [class.invalid]="sectionErrors()[index]?.isValid === false">
                  <div class="section-header">
                    <div class="drag-handle">⠿</div>
                    <span class="section-number">#{{ index + 1 }}</span>
                    <div class="reorder-buttons">
                      <button
                        (click)="moveSection(index, -1)"
                        [disabled]="index === 0"
                        class="btn-reorder"
                        aria-label="Flytta upp"
                      >↑</button>
                      <button
                        (click)="moveSection(index, 1)"
                        [disabled]="index === sections().length - 1"
                        class="btn-reorder"
                        aria-label="Flytta ner"
                      >↓</button>
                    </div>
                    <button (click)="removeSection(index)" class="btn-remove" aria-label="Ta bort">✕</button>
                  </div>

                  <div class="section-fields">
                    <div class="form-group">
                      <label for="label-{{ section.id }}">Rubrik <span class="required">*</span></label>
                      <input
                        id="label-{{ section.id }}"
                        type="text"
                        [formControl]="getControl(section.id, 'label')"
                        placeholder="T.ex. Smärtområde"
                      />
                      @if (getControl(section.id, 'label').invalid && getControl(section.id, 'label').touched) {
                        <span class="error">Rubrik krävs</span>
                      }
                    </div>

                    <div class="form-group">
                      <label for="type-{{ section.id }}">Typ</label>
                      <select
                        id="type-{{ section.id }}"
                        [formControl]="getControl(section.id, 'type')"
                        (change)="onTypeChange(section)"
                      >
                        <option value="text">Text (en rad)</option>
                        <option value="textarea">Text (flera rader)</option>
                        <option value="number">Numerisk</option>
                        <option value="select">Rullgardinsmeny</option>
                        <option value="multiselect">Flerval</option>
                        <option value="checkbox">Kryssruta</option>
                        <option value="date">Datum</option>
                        <option value="bodymap">Kroppskarta</option>
                        <option value="anatomy-map">Anatomikarta</option>
                      </select>
                    </div>

                    @if (isOptionType(section.type)) {
                      <div class="form-group">
                        <label for="options-{{ section.id }}">Alternativ (kommaseparerade) <span class="required">*</span></label>
                        <input
                          id="options-{{ section.id }}"
                          type="text"
                          [formControl]="getControl(section.id, 'options')"
                          placeholder="T.ex. Höger,Vänster,Båda"
                        />
                        @if (getControl(section.id, 'options').invalid && getControl(section.id, 'options').touched) {
                          <span class="error">Alternativ krävs</span>
                        }
                      </div>
                    }

                    @if (section.type === 'number') {
                      <div class="form-row">
                        <div class="form-group">
                          <label for="min-{{ section.id }}">Min</label>
                          <input
                            id="min-{{ section.id }}"
                            type="number"
                            [formControl]="getControl(section.id, 'min')"
                            placeholder="Valfritt"
                          />
                        </div>
                        <div class="form-group">
                          <label for="max-{{ section.id }}">Max</label>
                          <input
                            id="max-{{ section.id }}"
                            type="number"
                            [formControl]="getControl(section.id, 'max')"
                            placeholder="Valfritt"
                          />
                        </div>
                      </div>
                    }

                    @if (section.type === 'checkbox') {
                      <div class="form-group">
                        <label for="defaultValue-{{ section.id }}">Standardvärde</label>
                        <select
                          id="defaultValue-{{ section.id }}"
                          [formControl]="getControl(section.id, 'defaultValue')"
                        >
                          <option value="">Tom</option>
                          <option value="true">Satt</option>
                          <option value="false">Inte satt</option>
                        </select>
                      </div>
                    }

                    @if (section.type === 'anatomy-map') {
                      <p class="hint">Muskel- och skelettkartor ingår automatiskt i alla journaler. Lägg bara till extra anatomikartor här om du behöver ytterligare kartor med egna fyndalternativ.</p>
                      <div class="form-group">
                        <label for="preset-{{ section.id }}">Förinställd karta</label>
                        <select
                          id="preset-{{ section.id }}"
                          [formControl]="getControl(section.id, 'preset')"
                        >
                          <option value="horse-muscles-standard">Häst — muskler (standard)</option>
                          <option value="horse-skeleton-standard">Häst — skelett (standard)</option>
                        </select>
                      </div>
                      <div class="form-group">
                        <label for="findings-{{ section.id }}">Fyndalternativ (kommaseparerade) <span class="required">*</span></label>
                        <input
                          id="findings-{{ section.id }}"
                          type="text"
                          [formControl]="getControl(section.id, 'findingOptions')"
                          placeholder="Ua, Öm, Spänd, Svullen, Galla, Triggerpunkt, Sår, Knöl, Muskelknuta"
                        />
                        @if (getControl(section.id, 'findingOptions').invalid && getControl(section.id, 'findingOptions').touched) {
                          <span class="error">Minst ett fyndalternativ krävs</span>
                        }
                      </div>
                      <div class="form-group">
                        <label for="anatomy-img-{{ section.id }}">Egen bild (valfritt)</label>
                        <input
                          id="anatomy-img-{{ section.id }}"
                          type="file"
                          accept="image/jpeg,image/png"
                          (change)="onAnatomyImage($event, section)"
                        />
                        <p class="hint">PNG eller JPEG, max 5 MB. Samma layout som standardkartan antas.</p>
                        @if (anatomyImageBusy() === section.id) {
                          <span class="hint">Laddar upp…</span>
                        }
                        @if (anatomyPreviewUrls()[section.id]; as preview) {
                          <img [src]="preview" alt="Förhandsvisning av egen anatomibild" class="anatomy-preview" />
                          <button type="button" class="btn-small" (click)="clearAnatomyImage(section)">Ta bort egen bild</button>
                        }
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          }
        </div>

        <!-- JSON preview / Form preview -->
        @if (previewMode()) {
          <div class="preview-panel">
            <div class="panel-header">
              <h3>Förhandsvisning - Formulär</h3>
            </div>
            <div class="preview-content">
              @for (section of sections(); track section.id) {
                <div class="preview-field">
                  <label>{{ section.label || '(Ingen rubrik)' }}</label>
                  @switch (section.type) {
                    @case ('text') {
                      <input type="text" readonly placeholder="..." />
                    }
                    @case ('textarea') {
                      <textarea readonly rows="3" placeholder="..."></textarea>
                    }
                    @case ('number') {
                      <input type="number" readonly placeholder="..." />
                    }
                    @case ('select') {
                      <select readonly>
                        <option value="">Välj...</option>
                        @if (section.options) {
                          @for (opt of getOptions(section); track opt) {
                            <option>{{ opt }}</option>
                          }
                        }
                      </select>
                    }
                    @case ('multiselect') {
                      <select readonly multiple>
                        @if (section.options) {
                          @for (opt of getOptions(section); track opt) {
                            <option>{{ opt }}</option>
                          }
                        }
                      </select>
                    }
                    @case ('checkbox') {
                      <input type="checkbox" readonly disabled />
                    }
                    @case ('date') {
                      <input type="date" readonly />
                    }
                    @case ('bodymap') {
                      <div class="bodymap-placeholder">Kroppskarta (interaktiv)</div>
                    }
                    @case ('anatomy-map') {
                      <app-anatomy-map [readonly]="true" [presetId]="section.preset || 'horse-muscles-standard'" />
                    }
                  }
                </div>
              }
              @if (sections().length === 0) {
                <div class="empty-hint">Inga sektioner för förhandsvisning.</div>
              }
            </div>

            <div class="panel-header" style="margin-top: 1rem;">
              <h3>JSON-utdata</h3>
              <button (click)="copyJson()" class="btn-small">Kopiera</button>
            </div>
            <div class="json-preview">
              <pre>{{ templateJson() }}</pre>
            </div>
          </div>
        }
      </div>

      <footer class="builder-footer">
        <button type="button" (click)="validateAndClose()" class="btn-primary">
          Spara mall och stäng
        </button>
      </footer>
    </div>
  `,
  styles: [`
    .template-builder {
      display: flex;
      flex-direction: column;
      min-height: 500px;
      color: var(--color-text);
      background: var(--color-surface);
    }
    .builder-header, .builder-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      padding: 0.85rem 1.1rem;
      border-bottom: 1px solid var(--color-border);
    }
    .builder-footer { border-top: 1px solid var(--color-border); border-bottom: 0; }
    .builder-header h2 { margin: 0; font-size: 1.25rem; color: var(--color-primary-text); }
    .builder-actions { display: flex; align-items: center; gap: 0.5rem; }
    .btn-close { font-size: 1.5rem; min-width: 44px; padding: 0; }
    .builder-body { display: grid; gap: 1rem; padding: 1.1rem; }
    .panel-header { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; margin-bottom: 0.75rem; }
    .panel-header h3 { margin: 0; font-size: 1rem; }
    .sections-list { display: grid; gap: 0.85rem; }
    .section-card {
      background: var(--color-surface-muted);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.9rem 1rem 1rem;
    }
    .section-card.invalid { border-color: var(--color-danger); }
    .section-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.85rem;
    }
    .section-number { font-weight: 700; color: var(--color-text-muted); }
    .drag-handle { color: var(--color-text-muted); cursor: default; }
    .reorder-buttons { display: flex; gap: 0.25rem; }
    .btn-reorder, .btn-remove {
      min-width: 36px;
      min-height: 36px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      border-radius: var(--radius-sm);
      cursor: pointer;
    }
    .btn-remove { margin-left: auto; color: var(--color-danger); }
    .section-fields { display: grid; gap: 0.85rem; }
    .form-row { display: grid; gap: 0.75rem; }
    @media (min-width: 640px) { .form-row { grid-template-columns: 1fr 1fr; } }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; }
    .form-group label { font-weight: 600; font-size: 0.875rem; color: var(--color-text); }
    .form-group input, .form-group select, .form-group textarea {
      width: 100%;
      box-sizing: border-box;
      min-height: var(--tap-min);
      padding: 0.55rem 0.75rem;
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-sm);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
    }
    .required { color: var(--color-danger); }
    .error { color: var(--color-danger); font-size: 0.8125rem; }
    .json-preview { background: #1e1e1e; color: #d4d4d4; padding: 1rem; overflow: auto; max-height: 360px; border-radius: var(--radius-sm); }
    .btn-small { min-height: 36px; }
    .empty-hint { color: var(--color-text-muted); padding: 1.5rem; text-align: center; }
    .hint { color: var(--color-text-muted); font-size: 0.8rem; margin: 0.25rem 0 0; }
    .anatomy-preview { display: block; max-width: 220px; margin-top: 0.5rem; border: 1px solid var(--color-border); border-radius: 4px; }
    .preview-content { display: grid; gap: 0.75rem; }
    .preview-field { display: flex; flex-direction: column; gap: 0.3rem; }
    .preview-field label { font-weight: 600; font-size: 0.875rem; }
    .preview-field input, .preview-field select, .preview-field textarea {
      width: 100%; box-sizing: border-box; min-height: var(--tap-min);
      padding: 0.55rem 0.75rem; border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-sm); background: var(--color-surface); color: var(--color-text);
    }
    .bodymap-placeholder {
      padding: 1rem; border: 1px dashed var(--color-border-strong);
      border-radius: var(--radius-sm); color: var(--color-text-muted); text-align: center;
    }
  `]
})
export class TemplateBuilderComponent implements OnInit {
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  private confirm = inject(ConfirmService);
  private http = inject(HttpClient);

  initialTemplate = input<string>('');
  templateChange = output<string>();
  close = output<void>();

  sectionForms = signal<Map<string, FormGroup>>(new Map());
  sections = signal<TemplateSection[]>([]);
  templateJson = signal('');
  previewMode = signal(false);
  sectionErrors = signal<{ isValid: boolean | null }[]>([]);
  anatomyImageBusy = signal<string | null>(null);
  anatomyPreviewUrls = signal<Record<string, string>>({});

  private typeOptions = ['text', 'textarea', 'number', 'select', 'multiselect', 'checkbox', 'date', 'bodymap', 'anatomy-map'];

  ngOnInit(): void {
    if (this.initialTemplate()) {
      try {
        const parsed: TemplateData = JSON.parse(this.initialTemplate());
        if (parsed.sections && Array.isArray(parsed.sections)) {
          this.sections.set(parsed.sections.map(s => ({
            id: s.id || generateId(),
            key: s.key || `section_${s.id || generateId()}`,
            label: s.label,
            type: s.type,
            options: Array.isArray(s.options) ? (s.options as unknown as string[]).join(', ') : s.options,
            minValue: s.minValue,
            maxValue: s.maxValue,
            preset: s.preset || 'horse-muscles-standard',
            findingOptions: Array.isArray((s as TemplateSection & { findingOptions?: string[] }).findingOptions)
              ? ((s as TemplateSection & { findingOptions?: string[] }).findingOptions as unknown as string[]).join(', ')
              : (s.findingOptions || DEFAULT_FINDINGS),
            customImageKey: s.customImageKey || ''
          })));
        }
      } catch {
        // Invalid JSON, start empty
      }
    }
    this.buildFormMap();
    this.updateJson();
  }

  buildFormMap(): void {
    const map = new Map<string, FormGroup>();
    this.sections().forEach(section => {
      const form = this.fb.group({
        label: [section.label || '', Validators.required],
        type: [section.type, Validators.required],
        options: [section.options || '', section.type === 'select' || section.type === 'multiselect' ? Validators.required : Validators.nullValidator],
        min: [section.minValue ?? ''],
        max: [section.maxValue ?? ''],
        defaultValue: [''],
        preset: [section.preset || 'horse-muscles-standard'],
        findingOptions: [section.findingOptions || DEFAULT_FINDINGS, section.type === 'anatomy-map' ? Validators.required : Validators.nullValidator],
        customImageKey: [section.customImageKey || '']
      });
      map.set(section.id, form);
      if (section.customImageKey) this.resolveAnatomyPreview(section.id, section.customImageKey);
    });
    this.sectionForms.set(map);
  }

  getControl(sectionId: string, field: string): FormGroup {
    return this.sectionForms().get(sectionId)!.get(field) as FormGroup;
  }

  addSection(): void {
    const id = generateId();
    const newSection: TemplateSection = {
      id,
      key: `section_${id}`,
      label: '',
      type: 'text',
      options: '',
      minValue: null,
      maxValue: null,
      preset: 'horse-muscles-standard',
      findingOptions: DEFAULT_FINDINGS,
      customImageKey: ''
    };
    this.sections.update(sections => [...sections, newSection]);
    const map = new Map(this.sectionForms());
    const form = this.fb.group({
      label: ['', Validators.required],
      type: ['text', Validators.required],
      options: ['', Validators.nullValidator],
      min: [''],
      max: [''],
      defaultValue: [''],
      preset: ['horse-muscles-standard'],
      findingOptions: [DEFAULT_FINDINGS],
      customImageKey: ['']
    });
    map.set(newSection.id, form);
    this.sectionForms.set(map);
    this.updateJson();
  }

  async removeSection(index: number): Promise<void> {
    const sections = this.sections();
    if (index < 0 || index >= sections.length) return;
    const ok = await this.confirm.confirm({
      title: 'Ta bort sektion',
      message: 'Sektionen tas bort från mallen.',
      confirmLabel: 'Ta bort',
      destructive: true
    });
    if (!ok) return;

    const sectionId = sections[index].id;
    this.sections.update(s => s.filter((_, i) => i !== index));

    const map = new Map(this.sectionForms());
    map.delete(sectionId);
    this.sectionForms.set(map);
    this.updateJson();
  }

  moveSection(index: number, direction: number): void {
    const sections = this.sections();
    const newIndex = index + direction;
    if (newIndex < 0 || newIndex >= sections.length) return;

    this.sections.update(s => {
      const copy = [...s];
      [copy[index], copy[newIndex]] = [copy[newIndex], copy[index]];
      return copy;
    });

    // Reorder form map
    const map = new Map(this.sectionForms());
    const entries = Array.from(map.entries());
    const oldOrder = entries.map(e => e[0]);
    const newOrder = [...oldOrder];
    [newOrder[index], newOrder[newIndex]] = [newOrder[newIndex], newOrder[index]];

    const newMap = new Map<string, FormGroup>();
    newOrder.forEach(id => {
      newMap.set(id, map.get(id)!);
    });
    this.sectionForms.set(newMap);
    this.updateJson();
  }

  onTypeChange(section: TemplateSection): void {
    const form = this.sectionForms().get(section.id);
    const newType = (form?.get('type')?.value ?? section.type) as TemplateSection['type'];
    this.sections.update(list => list.map(s => s.id === section.id ? { ...s, type: newType } : s));
    if (form) {
      form.get('options')?.clearValidators();
      form.get('findingOptions')?.clearValidators();
      if (newType === 'select' || newType === 'multiselect') {
        form.get('options')?.setValidators([Validators.required]);
      }
      if (newType === 'anatomy-map') {
        form.get('findingOptions')?.setValidators([Validators.required]);
        if (!form.get('findingOptions')?.value) {
          form.get('findingOptions')?.setValue(DEFAULT_FINDINGS);
        }
        if (!form.get('preset')?.value) {
          form.get('preset')?.setValue('horse-muscles-standard');
        }
      }
      form.get('options')?.updateValueAndValidity();
      form.get('findingOptions')?.updateValueAndValidity();
    }
    this.updateJson();
  }

  isOptionType(type: string): boolean {
    return type === 'select' || type === 'multiselect';
  }

  getOptions(section: TemplateSection): string[] {
    if (!section.options) return [];
    return section.options.split(',').map(o => o.trim()).filter(o => o.length > 0);
  }

  updateJson(): void {
    const sections: TemplateSection[] = this.sections().map(s => {
      const form = this.sectionForms().get(s.id);
      const value = form?.value || {};

      return {
        id: s.id,
        key: s.key || `section_${Date.now()}`,
        label: value.label || s.label,
        type: value.type || s.type,
        options: (value.options !== '' && value.options != null) ? value.options : undefined,
        minValue: value.min !== '' && value.min != null ? Number(value.min) : null,
        maxValue: value.max !== '' && value.max != null ? Number(value.max) : null,
        preset: value.preset || s.preset,
        findingOptions: value.findingOptions || s.findingOptions,
        customImageKey: value.customImageKey || s.customImageKey
      };
    });

    const template = {
      version: 1,
      sections: sections.map(s => {
        const key = s.key || s.label.toLowerCase().replace(/\s+/g, '_');
        if (s.type === 'anatomy-map') {
          const findings = splitCsv(s.findingOptions);
          return {
            key,
            label: s.label,
            type: s.type,
            preset: s.preset || 'horse-muscles-standard',
            findingOptions: findings.length ? findings : DEFAULT_FINDING_OPTIONS,
            customImageKey: s.customImageKey || null
          };
        }
        return {
          key,
          label: s.label,
          type: s.type,
          options: typeof s.options === 'string'
            ? splitCsv(s.options)
            : s.options,
          min: s.minValue ?? undefined,
          max: s.maxValue ?? undefined
        };
      })
    };
    const json = JSON.stringify(template, null, 2);
    this.templateJson.set(json);
  }

  togglePreview(): void {
    this.previewMode.update(p => !p);
  }

  copyJson(): void {
    navigator.clipboard.writeText(this.templateJson()).then(() => {
      this.toast.success('JSON kopierad.');
    }).catch(() => {
      // Fallback: select the text
      const pre = document.querySelector('.json-preview pre');
      if (pre) {
        const range = document.createRange();
        range.selectNodeContents(pre);
        const sel = window.getSelection();
        sel?.removeAllRanges();
        sel?.addRange(range);
      }
    });
  }

  validate(): boolean {
    const sections = this.sections();
    if (sections.length === 0) {
      return false;
    }

    const errors = sections.map((section) => {
      const form = this.sectionForms().get(section.id);
      if (!form) return { isValid: false };

      form.markAllAsTouched();
      const isValid = form.valid;

      const type = (form.get('type')?.value ?? section.type) as string;
      if ((type === 'select' || type === 'multiselect') && !form.get('options')?.value) {
        return { isValid: false };
      }
      if (type === 'anatomy-map' && !splitCsv(form.get('findingOptions')?.value).length) {
        return { isValid: false };
      }

      return { isValid };
    });

    this.sectionErrors.set(errors);

    const allValid = errors.every(e => e.isValid === true);

    if (!allValid) {
      // Scroll to first invalid section
      const firstInvalid = errors.findIndex(e => e.isValid === false);
      if (firstInvalid >= 0) {
        const firstInvalidEl = document.querySelectorAll('.section-card')[firstInvalid];
        firstInvalidEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }

    return allValid;
  }

  validateAndClose(): void {
    this.updateJson();
    if (!this.validate()) {
      return;
    }

    this.templateChange.emit(this.templateJson());
    this.close.emit();
  }

  onAnatomyImage(event: Event, section: TemplateSection): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      this.toast.error('Bilden får vara högst 5 MB.');
      return;
    }
    const data = new FormData();
    data.append('file', file);
    this.anatomyImageBusy.set(section.id);
    this.http.post<{ key?: string; Key?: string; url?: string; Url?: string }>(
      `${API_URL}/api/app/settings/anatomy-images`,
      data
    ).subscribe({
      next: r => {
        const key = r.key ?? r.Key ?? '';
        const url = r.url ?? r.Url;
        this.sectionForms().get(section.id)?.get('customImageKey')?.setValue(key);
        if (url) this.anatomyPreviewUrls.update(m => ({ ...m, [section.id]: url }));
        this.anatomyImageBusy.set(null);
        this.updateJson();
      },
      error: () => {
        this.anatomyImageBusy.set(null);
        this.toast.error('Kunde inte ladda upp bilden.');
      }
    });
  }

  clearAnatomyImage(section: TemplateSection): void {
    this.sectionForms().get(section.id)?.get('customImageKey')?.setValue('');
    this.anatomyPreviewUrls.update(m => {
      const next = { ...m };
      delete next[section.id];
      return next;
    });
    this.updateJson();
  }

  private resolveAnatomyPreview(sectionId: string, key: string): void {
    this.http.get<{ url?: string; Url?: string }>(
      `${API_URL}/api/app/settings/anatomy-images/url`,
      { params: { key } }
    ).subscribe({
      next: r => {
        const url = r.url ?? r.Url;
        if (url) this.anatomyPreviewUrls.update(m => ({ ...m, [sectionId]: url }));
      }
    });
  }
}

function splitCsv(value: unknown): string[] {
  if (Array.isArray(value)) return value.map(v => String(v).trim()).filter(Boolean);
  if (typeof value !== 'string' || !value.trim()) return [];
  return value.split(',').map(o => o.trim()).filter(Boolean);
}
