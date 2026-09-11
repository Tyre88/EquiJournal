import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';

interface TreatmentRow {
  id: string;
  name: string;
  slug: string;
  bookableOnline: boolean;
  requiresApproval: boolean;
  status: string;
}

interface WidgetSettings {
  allowedOrigins: string[];
  showPrices: boolean;
  bookingTerms: string;
  privacyPolicyUrl: string;
  verificationWindowMinutes: number;
  cancellationNoticeHours: number;
  publicBaseUrl: string;
  contactPhone: string;
  contactEmail: string;
  treatments: TreatmentRow[];
  embedSnippet: string;
  treatmentSnippets: { id: string; name: string; slug: string; snippet: string }[];
}

@Component({
  selector: 'app-widget-settings',
  standalone: true,
  imports: [FormsModule, RouterLink, EjPageHeaderComponent],
  template: `
    <div class="page page--narrow">
      <ej-page-header title="Widget" subtitle="Konfigurera och bädda in den publika bokningen." />
      <div class="card">
        <h2 class="section-title">Så här använder du widgeten</h2>
        <ol>
          <li>Lägg till webbplatsens adress under tillåtna webbplatser (t.ex. https://din-sajt.se).</li>
          <li>Kopiera inbäddningskoden nedan.</li>
          <li>I WordPress: infoga ett <strong>Anpassad HTML</strong>-block och klistra in koden.</li>
          <li>Publicera sidan och testa på telefon.</li>
        </ol>
        <p><a routerLink="/booking-requests">Öppna inkomna förfrågningar</a></p>
      </div>
      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (s(); as settings) {
        <div class="card">
          <label class="field-label" for="origins">Tillåtna webbplatser (en per rad)</label>
          <textarea id="origins" class="input" rows="4" [(ngModel)]="originsText"></textarea>
        </div>
        <div class="card">
          <label class="checkbox-label">
            <input type="checkbox" [(ngModel)]="settings.showPrices" /> Visa priser i widgeten
          </label>
          <div class="field">
            <label class="field-label" for="terms">Bokningsvillkor</label>
            <textarea id="terms" class="input" rows="5" [(ngModel)]="settings.bookingTerms"></textarea>
          </div>
          <div class="field">
            <label class="field-label" for="privacy">Integritetspolicy (URL)</label>
            <input id="privacy" class="input" [(ngModel)]="settings.privacyPolicyUrl" />
          </div>
          <div class="field">
            <label class="field-label" for="window">Verifieringsfönster (minuter)</label>
            <input id="window" class="input" type="number" [(ngModel)]="settings.verificationWindowMinutes" />
          </div>
          <div class="field">
            <label class="field-label" for="notice">Avbokningsfrist (timmar)</label>
            <input id="notice" class="input" type="number" [(ngModel)]="settings.cancellationNoticeHours" />
          </div>
          <div class="field">
            <label class="field-label" for="base">Publik bas-URL</label>
            <input id="base" class="input" [(ngModel)]="settings.publicBaseUrl" />
          </div>
          <div class="field">
            <label class="field-label" for="phone">Telefon vid fel</label>
            <input id="phone" class="input" [(ngModel)]="settings.contactPhone" />
          </div>
          <div class="field">
            <label class="field-label" for="email">E-post vid fel</label>
            <input id="email" class="input" [(ngModel)]="settings.contactEmail" />
          </div>
          <button type="button" class="btn-primary" (click)="save()">Spara</button>
        </div>
        <div class="card">
          <h2 class="section-title">Behandlingar online</h2>
          <ul class="plain-list">
            @for (t of settings.treatments; track t.id) {
              <li>
                {{ t.name }}
                <label class="checkbox-label">
                  <input type="checkbox" [ngModel]="t.bookableOnline" (ngModelChange)="toggle(t, 'bookableOnline', $event)" /> Synlig
                </label>
                <label class="checkbox-label">
                  <input type="checkbox" [ngModel]="t.requiresApproval" (ngModelChange)="toggle(t, 'requiresApproval', $event)" /> Kräver godkännande
                </label>
                <a [routerLink]="['/treatment-types', t.id, 'edit']">Redigera</a>
              </li>
            }
          </ul>
        </div>
        <div class="card">
          <h2 class="section-title">Inbäddningskod</h2>
          <pre class="snippet">{{ settings.embedSnippet }}</pre>
          <button type="button" class="btn-secondary" (click)="copy(settings.embedSnippet)">Kopiera</button>
          @for (snip of settings.treatmentSnippets ?? []; track snip.id) {
            <h3>{{ snip.name }}</h3>
            <pre class="snippet">{{ snip.snippet }}</pre>
            <button type="button" class="btn-secondary" (click)="copy(snip.snippet)">Kopiera {{ snip.slug }}</button>
          }
        </div>
        <div class="card">
          <h2 class="section-title">Förhandsgranskning</h2>
          <a class="btn-secondary" [href]="previewUrl()" target="_blank" rel="noopener">Öppna widgeten</a>
          @if (previewSrc()) {
            <iframe class="preview" [src]="previewSrc()" title="Widgetförhandsgranskning"></iframe>
          }
        </div>
      }
    </div>
  `,
  styles: [`.snippet { white-space: pre-wrap; background: var(--color-surface-muted); padding: 1rem; border-radius: 8px; } .plain-list { padding-left: 1.1rem; } .preview { width: 100%; min-height: 420px; border: 1px solid var(--color-border); border-radius: 8px; }`]
})
export class WidgetSettingsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  private sanitizer = inject(DomSanitizer);
  s = signal<WidgetSettings | null>(null);
  originsText = '';
  error = signal('');
  previewUrl = signal('');
  previewSrc = signal<SafeResourceUrl | null>(null);

  ngOnInit(): void {
    this.api.get<WidgetSettings>('/api/app/widget-settings').subscribe({
      next: (value) => {
        this.s.set(value);
        this.originsText = (value.allowedOrigins ?? []).join('\n');
        this.setPreview((value.publicBaseUrl || '').replace(/\/$/, '') + '/widget/');
      },
      error: () => this.error.set('Kunde inte ladda inställningarna.')
    });
  }

  private setPreview(url: string): void {
    this.previewUrl.set(url);
    this.previewSrc.set(url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null);
  }

  save(): void {
    const current = this.s();
    if (!current) return;
    const allowedOrigins = this.originsText.split(/\r?\n/).map(x => x.trim()).filter(Boolean);
    this.api.put<WidgetSettings>('/api/app/widget-settings', { ...current, allowedOrigins }).subscribe({
      next: (value) => {
        this.s.set({ ...current, ...value, treatments: current.treatments });
        this.originsText = (value.allowedOrigins ?? []).join('\n');
        this.setPreview((value.publicBaseUrl || '').replace(/\/$/, '') + '/widget/');
        this.toast.success('Inställningarna sparades.');
      },
      error: () => this.error.set('Kunde inte spara.')
    });
  }

  toggle(t: TreatmentRow, field: 'bookableOnline' | 'requiresApproval', value: boolean): void {
    t[field] = value;
    this.api.put(`/api/app/treatment-types/${t.id}`, { [field]: value }).subscribe({
      error: () => this.toast.error('Kunde inte uppdatera behandlingen.')
    });
  }

  copy(snippet: string): void {
    void navigator.clipboard.writeText(snippet);
    this.toast.success('Koden kopierades.');
  }
}
