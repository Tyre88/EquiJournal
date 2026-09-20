import { Component, OnInit, DestroyRef, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { EntityHistoryComponent } from '../../audit/entity-history.component';
import { AnatomyMapComponent } from '../anatomy-map/anatomy-map.component';
import { AnatomyMapValue, isAnatomyMapValue, parseAnatomyMapValue } from '../anatomy-map/anatomy-map.types';
import {
  STANDARD_ANATOMY_SECTIONS,
  STANDARD_SECTION_LABELS
} from '../journal-standard-sections';
import { environment } from '../../../environments/environment';
const API_URL = environment.apiUrl;

interface AttachmentItem {
  id: string;
  name: string;
  mimeType: string;
  size: number;
  uploadedAt: string;
  downloadUrl?: string;
}

interface AmendmentItem {
  id: string;
  text: string;
  reason: string;
  createdAt: string;
  createdBy: string;
}

interface JournalData {
  id: string;
  horseId: string;
  horseName: string;
  ownerName: string;
  treatmentTypeId: string;
  treatmentTypeName: string;
  performedAt: string;
  status: string;
  anamnes: string;
  statusKlinisk: string;
  atgarder: string;
  diagnos?: string;
  differentialdiagnoser?: string;
  prognosOchPlan?: string;
  templateData: Record<string, any>;
  attachments: AttachmentItem[];
  amendments: AmendmentItem[];
  createdAt: string;
  updatedAt: string;
  signedAt: string;
  signedBy: string;
}

@Component({
  selector: 'app-journal-view',
  standalone: true,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, AnatomyMapComponent, EjPageHeaderComponent, EntityHistoryComponent],
  template: `
    <div class="page">
      <ej-page-header title="Journal" backHref="/journals">
        @if (journal()?.status === 'Draft') {
          <a class="btn-secondary" [routerLink]="['/journals', journal()!.id, 'edit']">Redigera</a>
        }
        <button type="button" class="btn-secondary" (click)="exportPdf()">PDF</button>
        @if (journal()?.status === 'Signed') {
          <button type="button" class="btn-primary" (click)="toggleAmendmentForm()">Lägg till tillägg</button>
        }
      </ej-page-header>

      @if (loading()) {
        <p class="muted">Laddar journal...</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else {
      @if (journal(); as j) {
        <section class="card hero">
          <div class="hero-top">
            <h2>{{ j.horseName }}</h2>
            <span [class]="j.status === 'Signed' ? 'badge badge-signed' : 'badge badge-draft'">{{ statusText(j.status) }}</span>
          </div>
          <p class="owner">Ägare: {{ j.ownerName }}</p>
          <dl class="meta">
            <div>
              <dt>Behandling</dt>
              <dd>{{ j.treatmentTypeName }}</dd>
            </div>
            <div>
              <dt>Utförd</dt>
              <dd>{{ formatDate(j.performedAt) }}</dd>
            </div>
            @if (j.signedAt) {
              <div>
                <dt>Signerad</dt>
                <dd>{{ formatDate(j.signedAt) }}@if (j.signedBy) { <span> · {{ j.signedBy }}</span> }</dd>
              </div>
            }
          </dl>
        </section>

        <app-entity-history entityType="JournalEntry" [entityId]="j.id" />

        <section class="card">
          <h3>Basfält</h3>
          <div class="field required">
            <div class="label">Anamnes</div>
            <div class="value">{{ j.anamnes || '—' }}</div>
          </div>
          <div class="field required">
            <div class="label">Status klinisk</div>
            <div class="value">{{ j.statusKlinisk || '—' }}</div>
          </div>
          <div class="field required">
            <div class="label">Undersökning / behandling och motivering</div>
            <div class="value">{{ j.atgarder || '—' }}</div>
          </div>
          @if (j.diagnos) {
            <div class="field">
              <div class="label">Diagnos</div>
              <div class="value">{{ j.diagnos }}</div>
            </div>
          }
          @if (j.differentialdiagnoser) {
            <div class="field">
              <div class="label">Differentialdiagnoser</div>
              <div class="value">{{ j.differentialdiagnoser }}</div>
            </div>
          }
          <div class="field">
            <div class="label">Prognos och plan / hemgångsråd</div>
            <div class="value">{{ j.prognosOchPlan || '—' }}</div>
          </div>
        </section>

        <section class="card">
          <h3>Anatomikartor</h3>
          @for (section of standardAnatomySections; track section.key) {
            <div class="field">
              <div class="label">{{ section.label }}</div>
              @if (standardAnatomyValue(section.key); as anatomy) {
                <app-anatomy-map
                  [presetId]="section.preset"
                  [annotations]="anatomy.annotations"
                  [strokes]="anatomy.strokes"
                  [readonly]="true"
                />
              }
            </div>
          }
        </section>

        @if (templateEntries().length > 0) {
          <section class="card">
            <h3>Mallfält</h3>
            @for (entry of templateEntries(); track entry.key) {
              <div class="field">
                <div class="label">{{ entry.label }}</div>
                @if (templateMapValue(entry.value); as anatomy) {
                  <app-anatomy-map
                    [presetId]="anatomy.preset"
                    [customImageUrl]="anatomyImageUrls()[entry.key] || null"
                    [annotations]="anatomy.annotations"
                    [strokes]="anatomy.strokes"
                    [readonly]="true"
                  />
                } @else {
                  <div class="value">{{ formatTemplateValue(entry.value) }}</div>
                }
              </div>
            }
          </section>
        }

        <section class="card">
          <div class="card-head">
            <h3>Bilagor ({{ j.attachments.length }})</h3>
            @if (j.status === 'Signed') {
              <label class="btn">
                Ladda upp
                <input type="file" hidden (change)="onUpload($event)" />
              </label>
            }
          </div>
          @if (j.attachments.length === 0) {
            <p class="muted">Inga bilagor.</p>
          } @else {
            <ul class="files">
              @for (att of j.attachments; track att.id) {
                <li>
                  <button type="button" class="link" (click)="openAttachment(j.id, att)">{{ att.name }}</button>
                  <span class="muted">{{ formatSize(att.size) }}</span>
                </li>
              }
            </ul>
          }
        </section>

        <section class="card">
          <h3>Tillägg ({{ j.amendments.length }})</h3>
          @if (j.amendments.length === 0) {
            <p class="muted">Inga tillägg.</p>
          } @else {
            @for (amend of j.amendments; track amend.id) {
              <article class="amendment">
                <header>
                  <time>{{ formatDate(amend.createdAt) }}</time>
                  <span>{{ amend.createdBy }}</span>
                </header>
                <p>{{ amend.text }}</p>
                @if (amend.reason) {
                  <p class="reason">Anledning: {{ amend.reason }}</p>
                }
              </article>
            }
          }
        </section>
      }
      }

      @if (showAmendmentForm()) {
        <div class="confirm-overlay" (click)="toggleAmendmentForm()">
          <div class="confirm-dialog" (click)="$event.stopPropagation()">
            <header><h2>Lägg till tillägg</h2></header>
            <form class="body" [formGroup]="amendmentForm" (ngSubmit)="submitAmendment()">
              <div class="field">
                <label class="field-label required" for="amend-text">Tillägg</label>
                <textarea id="amend-text" class="input" formControlName="text" rows="4"></textarea>
                @if (amendText?.invalid && amendText?.touched) { <span class="field-error">Text krävs</span> }
              </div>
              <div class="field">
                <label class="field-label required" for="amend-reason">Anledning</label>
                <textarea id="amend-reason" class="input" formControlName="reason" rows="2"></textarea>
                @if (amendReason?.invalid && amendReason?.touched) { <span class="field-error">Anledning krävs</span> }
              </div>
              <footer>
                <button type="button" class="btn-secondary" (click)="toggleAmendmentForm()">Avbryt</button>
                <button type="submit" class="btn-primary" [disabled]="amendmentForm.invalid || savingAmendment()">
                  {{ savingAmendment() ? 'Sparar…' : 'Spara tillägg' }}
                </button>
              </footer>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .hero-top { display: flex; justify-content: space-between; gap: 0.75rem; align-items: center; }
    .hero-top h2 { margin: 0; }
    .meta { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 0.75rem; }
    .meta dt { color: var(--color-text-muted); font-size: 0.8rem; font-weight: 600; }
    .meta dd { margin: 0; }
    .value { white-space: pre-wrap; }
    .files { list-style: none; padding: 0; margin: 0; }
    .files li { display: flex; justify-content: space-between; gap: 0.75rem; padding: 0.45rem 0; }
    .amendment { padding: 0.75rem 0; border-top: 1px solid var(--color-border); }
    .card-head { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; }
  `]
})
export class JournalViewComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);
  private toast = inject(ToastService);

  journal = signal<JournalData | null>(null);
  loading = signal(true);
  error = signal('');
  showAmendmentForm = signal(false);
  savingAmendment = signal(false);
  anatomyImageUrls = signal<Record<string, string>>({});

  amendmentForm = this.fb.group({
    text: ['', [Validators.required]],
    reason: ['', [Validators.required]]
  });

  readonly standardAnatomySections = STANDARD_ANATOMY_SECTIONS;

  templateEntries = computed(() => {
    const j = this.journal();
    if (!j?.templateData) return [];
    return Object.entries(j.templateData)
      .filter(([key]) => !STANDARD_SECTION_LABELS[key])
      .map(([key, value]) => ({
        key,
        label: STANDARD_SECTION_LABELS[key]
          ?? key.replace(/([A-Z])/g, ' $1').replace(/^./, s => s.toUpperCase()),
        value
      }));
  });

  standardAnatomyValue(key: string): AnatomyMapValue {
    const raw = this.journal()?.templateData?.[key];
    const section = STANDARD_ANATOMY_SECTIONS.find(s => s.key === key);
    return parseAnatomyMapValue(raw, section?.preset);
  }

  get amendText() { return this.amendmentForm.get('text'); }
  get amendReason() { return this.amendmentForm.get('reason'); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/journals']);
      return;
    }
    this.loadJournal(id);
  }

  private loadJournal(id: string): void {
    this.http.get<any>(`${API_URL}/api/app/journals/${id}`).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (response: any) => {
        const j = response;
        this.journal.set({
          id: j.id,
          horseId: j.horseId || j.horse?.id || '',
          horseName: j.horseName || j.horse?.name || 'Okänd häst',
          ownerName: j.ownerName || j.horse?.ownerName || 'Okänd',
          treatmentTypeId: j.treatmentTypeId || j.treatmentType?.id || '',
          treatmentTypeName: j.treatmentTypeName || j.treatmentType?.name || '—',
          performedAt: j.performedAt || j.performedAtLocal || '',
          status: j.status || 'Draft',
          anamnes: j.anamnes || '',
          statusKlinisk: j.statusKlinisk || '',
          atgarder: j.atgarder || '',
          diagnos: j.diagnos || '',
          differentialdiagnoser: j.differentialdiagnoser || '',
          prognosOchPlan: j.prognosOchPlan || '',
          templateData: parseTemplate(j.templateDataJson ?? j.templateData),
          attachments: (j.attachments || []).map((a: any) => ({
            id: a.id,
            name: a.originalFileName ?? a.name,
            mimeType: a.contentType ?? a.mimeType,
            size: a.size,
            uploadedAt: a.createdAt ?? a.uploadedAt,
            downloadUrl: a.signedUrl
          })),
          amendments: j.amendments || [],
          createdAt: j.createdAt || '',
          updatedAt: j.updatedAt || '',
          signedAt: j.signedAt || '',
          signedBy: j.signedBy || ''
        });
        this.resolveAnatomyImages(this.journal()!.templateData);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte hämta journalen.');
        this.loading.set(false);
      }
    });
  }

  toggleAmendmentForm(): void {
    this.showAmendmentForm.update(v => !v);
    if (!this.showAmendmentForm()) {
      this.amendmentForm.reset();
    }
  }

  submitAmendment(): void {
    if (this.amendmentForm.invalid) return;

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.savingAmendment.set(true);
    const { text, reason } = this.amendmentForm.value;

    this.http.post<any>(`${API_URL}/api/app/journals/${id}/amendments`, {
      text: text || '',
      reason: reason || ''
    }).subscribe({
      next: (response: any) => {
        const amend: AmendmentItem = response.amendment ?? {
          id: response.id,
          text: text || '',
          reason: reason || '',
          createdAt: response.createdAt || new Date().toISOString(),
          createdBy: response.createdBy || ''
        };
        this.journal.update(j => j ? ({ ...j, amendments: [...j.amendments, amend] }) : j);
        this.amendmentForm.reset();
        this.showAmendmentForm.set(false);
      },
      error: () => {
        this.toast.error('Misslyckades med att spara tillägget.');
      },
      complete: () => {
        this.savingAmendment.set(false);
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/journals']);
  }

  statusText(status: string): string {
    if (status === 'Signed') return 'Signerad';
    if (status === 'Draft') return 'Utkast';
    return status;
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '—';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('sv-SE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return dateStr;
    }
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  formatTemplateValue(value: any): string {
    if (value == null) return '—';
    if (typeof value === 'string' && value.startsWith('[')) {
      try {
        const arr = JSON.parse(value);
        if (Array.isArray(arr)) return arr.join(', ');
      } catch {
        // fall through
      }
    }
    return String(value);
  }

  templateMapValue(value: unknown): AnatomyMapValue | null {
    if (!isAnatomyMapValue(value)) return null;
    return parseAnatomyMapValue(value);
  }

  openAttachment(journalId: string, att: AttachmentItem): void {
    if (att.downloadUrl) {
      window.open(att.downloadUrl, '_blank', 'noopener');
      return;
    }
    this.http.get<{ signedUrl?: string; SignedUrl?: string }>(
      `${API_URL}/api/app/journals/${journalId}/attachments/${att.id}/download`
    ).subscribe({
      next: res => {
        const url = res.signedUrl ?? res.SignedUrl;
        if (url) window.open(url, '_blank', 'noopener');
      },
      error: () => this.toast.error('Kunde inte öppna bilagan.')
    });
  }

  onUpload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const id = this.journal()?.id;
    input.value = '';
    if (!file || !id) return;
    const form = new FormData();
    form.append('file', file);
    this.http.post<any>(`${API_URL}/api/app/journals/${id}/attachments`, form).subscribe({
      next: r => {
        const item: AttachmentItem = {
          id: r.id,
          name: r.originalFileName ?? r.name ?? file.name,
          mimeType: r.contentType ?? file.type,
          size: r.size ?? file.size,
          uploadedAt: new Date().toISOString(),
          downloadUrl: r.signedUrl ?? r.SignedUrl
        };
        this.journal.update(j => j ? { ...j, attachments: [...j.attachments, item] } : j);
      },
      error: () => this.toast.error('Kunde inte ladda upp filen.')
    });
  }

  exportPdf(): void {
    const j = this.journal();
    if (!j) return;
    this.http.post(`${API_URL}/api/app/journals/export/pdf/${j.id}`, {}, { responseType: 'blob' }).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `journal_${j.id}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  private resolveAnatomyImages(data: Record<string, unknown>): void {
    for (const [key, value] of Object.entries(data)) {
      if (!isAnatomyMapValue(value)) continue;
      const parsed = parseAnatomyMapValue(value);
      if (!parsed.customImageKey) continue;
      this.http.get<{ url?: string; Url?: string }>(
        `${API_URL}/api/app/settings/anatomy-images/url`,
        { params: { key: parsed.customImageKey } }
      ).subscribe({
        next: r => {
          const url = r.url ?? r.Url;
          if (url) this.anatomyImageUrls.update(m => ({ ...m, [key]: url }));
        }
      });
    }
  }
}

function parseTemplate(raw: unknown): Record<string, unknown> {
  if (!raw) return {};
  if (typeof raw === 'object') return raw as Record<string, unknown>;
  try { return JSON.parse(String(raw)); } catch { return {}; }
}

