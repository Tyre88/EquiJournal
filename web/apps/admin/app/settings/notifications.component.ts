import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';

interface Template {
  id: string;
  type: string;
  channel: string;
  subject: string;
  body: string;
}

interface NotificationSettings {
  enabled: Record<string, Record<string, boolean>>;
  reminderLeadHours: number;
  quietHoursStart: string;
  quietHoursEnd: string;
  dailySmsCap: number;
  morningSummaryTime: string;
  mergeFields: string[];
  templates: Template[];
}

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent],
  template: `
    <ej-page-header title="Aviseringar" subtitle="Kanaler, tyst tid och mallar." />
    @if (s(); as n) {
      <div class="card">
        <div class="field"><label class="field-label" for="lead">Påminnelse (timmar före)</label>
          <input id="lead" class="input" type="number" [(ngModel)]="n.reminderLeadHours" /></div>
        <div class="field"><label class="field-label" for="qs">Tyst tid från</label>
          <input id="qs" class="input" type="time" [(ngModel)]="n.quietHoursStart" /></div>
        <div class="field"><label class="field-label" for="qe">Tyst tid till</label>
          <input id="qe" class="input" type="time" [(ngModel)]="n.quietHoursEnd" /></div>
        <div class="field"><label class="field-label" for="cap">SMS-tak per dag</label>
          <input id="cap" class="input" type="number" [(ngModel)]="n.dailySmsCap" /></div>
        <div class="field"><label class="field-label" for="morning">Morgonsammanfattning</label>
          <input id="morning" class="input" type="time" [(ngModel)]="n.morningSummaryTime" /></div>
        <button type="button" class="btn-primary" (click)="saveSettings()">Spara inställningar</button>
      </div>
      <div class="card">
        <h2 class="section-title">Kanaler</h2>
        @for (type of types(); track type) {
          <div class="toggle-row">
            <strong>{{ type }}</strong>
            @for (ch of ['Email','Sms','InApp']; track ch) {
              <label class="checkbox-label">
                <input type="checkbox" [ngModel]="n.enabled[type]?.[ch] !== false" (ngModelChange)="setEnabled(type, ch, $event)" />
                {{ ch }}
              </label>
            }
          </div>
        }
      </div>
      <div class="card">
        <h2 class="section-title">Mallar</h2>
        <p class="muted">Tillåtna fält: {{ (n.mergeFields ?? []).join(', ') }}</p>
        <select class="input" [(ngModel)]="selectedId" (ngModelChange)="pick()">
          @for (t of n.templates; track t.id) {
            <option [value]="t.id">{{ t.type }} / {{ t.channel }}</option>
          }
        </select>
        @if (edit(); as t) {
          <div class="field"><label class="field-label" for="subj">Ämne</label>
            <input id="subj" class="input" [(ngModel)]="t.subject" /></div>
          <div class="field"><label class="field-label" for="body">Text</label>
            <textarea id="body" class="input" rows="8" [(ngModel)]="t.body"></textarea></div>
          @if (t.channel === 'Sms') {
            <p class="muted">{{ t.body.length }} tecken · {{ Math.ceil(t.body.length / 160) || 1 }} SMS-segment</p>
          }
          <div class="actions-bar">
            <button type="button" class="btn-secondary" (click)="preview()">Förhandsgranska</button>
            <button type="button" class="btn-primary" (click)="saveTemplate()">Spara mall</button>
          </div>
          @if (previewText()) { <pre class="snippet">{{ previewText() }}</pre> }
          @if (templateError()) { <div class="alert alert-error">{{ templateError() }}</div> }
        }
      </div>
    }
  `,
  styles: [`.toggle-row { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; margin-bottom:0.6rem; }`]
})
export class NotificationSettingsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  s = signal<NotificationSettings | null>(null);
  selectedId = '';
  edit = signal<Template | null>(null);
  previewText = signal('');
  templateError = signal('');
  Math = Math;

  types(): string[] {
    return Object.keys(this.s()?.enabled ?? {});
  }

  ngOnInit(): void {
    this.api.get<NotificationSettings>('/api/app/notification-settings').subscribe(v => {
      this.s.set(v);
      this.selectedId = v.templates[0]?.id ?? '';
      this.pick();
    });
  }

  setEnabled(type: string, ch: string, on: boolean): void {
    const current = this.s();
    if (!current) return;
    current.enabled[type] ??= {};
    current.enabled[type][ch] = on;
  }

  pick(): void {
    const t = this.s()?.templates.find(x => x.id === this.selectedId);
    this.edit.set(t ? { ...t } : null);
    this.previewText.set('');
    this.templateError.set('');
  }

  saveSettings(): void {
    const n = this.s();
    if (!n) return;
    this.api.put('/api/app/notification-settings', n).subscribe({
      next: () => this.toast.success('Aviseringsinställningar sparade.'),
      error: () => this.toast.error('Kunde inte spara.')
    });
  }

  saveTemplate(): void {
    const t = this.edit();
    if (!t) return;
    this.api.put(`/api/app/notification-settings/templates/${t.id}`, { subject: t.subject, body: t.body }).subscribe({
      next: () => {
        this.templateError.set('');
        this.toast.success('Mallen sparades.');
      },
      error: err => this.templateError.set(err?.error?.error || 'Ogiltig mall.')
    });
  }

  preview(): void {
    const t = this.edit();
    if (!t) return;
    this.api.post<{ subject: string; body: string }>('/api/app/notification-settings/templates/preview', {
      subject: t.subject,
      body: t.body
    }).subscribe({
      next: res => {
        this.previewText.set(`${res.subject}\n\n${res.body}`);
        this.templateError.set('');
      },
      error: err => this.templateError.set(err?.error?.error || 'Kunde inte förhandsgranska.')
    });
  }
}
