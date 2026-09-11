import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';

@Component({
  selector: 'app-marketing',
  standalone: true,
  imports: [FormsModule, EjPageHeaderComponent],
  template: `
    <div class="page page--narrow">
      <ej-page-header title="Utskick" subtitle="Skickas bara till kunder som gett marknadsföringssamtycke." />
      <p>Mottagare med samtycke: {{ audience() }}</p>
      <div class="card">
        <div class="field"><label class="field-label" for="subj">Ämne</label>
          <input id="subj" class="input" [(ngModel)]="subject" /></div>
        <div class="field"><label class="field-label" for="body">Meddelande</label>
          <textarea id="body" class="input" rows="8" [(ngModel)]="body"></textarea></div>
        <button type="button" class="btn-primary" (click)="send()">Köa utskick</button>
      </div>
    </div>
  `
})
export class MarketingComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  audience = signal(0);
  subject = '';
  body = '';

  ngOnInit(): void {
    this.api.get<{ consented: number }>('/api/app/marketing/audience').subscribe(v => this.audience.set(v.consented));
  }

  send(): void {
    this.api.post<{ queued: number }>('/api/app/marketing/send', { subject: this.subject, body: this.body }).subscribe({
      next: res => this.toast.success(`${res.queued} meddelanden köade.`),
      error: () => this.toast.error('Kunde inte skicka. Kräver ämne och text.')
    });
  }
}
