import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';

interface FollowUp {
  horseId: string;
  horseName: string;
  ownerName: string;
  lastTreatment: string;
  dueAt: string;
  daysOverdue: number;
}

@Component({
  selector: 'app-follow-ups',
  standalone: true,
  imports: [FormsModule, RouterLink, EjPageHeaderComponent],
  template: `
    <div class="page">
      <ej-page-header title="Uppföljningar" subtitle="Hästar där intervallet har passerat." />
      @for (row of rows(); track row.horseId) {
        <div class="card">
          <p><a [routerLink]="['/horses', row.horseId]">{{ row.horseName }}</a> · {{ row.ownerName }}</p>
          <p>{{ row.lastTreatment }} · {{ row.daysOverdue }} dagar sen</p>
          <div class="actions-bar">
            <button type="button" class="btn-primary" (click)="remind(row)">Skicka påminnelse</button>
            <select class="input" [(ngModel)]="snoozeWeeks[row.horseId]" aria-label="Snooza veckor">
              @for (w of [1,2,3,4,8,12]; track w) { <option [value]="w">{{ w }} v</option> }
            </select>
            <button type="button" class="btn-secondary" (click)="snooze(row)">Snooza</button>
            <input class="input" [(ngModel)]="reasons[row.horseId]" placeholder="Anledning att avfärda" />
            <button type="button" class="btn-danger" (click)="dismiss(row)">Avfärda</button>
          </div>
        </div>
      } @empty {
        <p>Inga uppföljningar just nu.</p>
      }
    </div>
  `
})
export class FollowUpsComponent implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);
  rows = signal<FollowUp[]>([]);
  reasons: Record<string, string> = {};
  snoozeWeeks: Record<string, number> = {};

  ngOnInit(): void {
    this.api.get<FollowUp[]>('/api/app/follow-ups').subscribe(v => {
      this.rows.set(v);
      for (const row of v) this.snoozeWeeks[row.horseId] = 2;
    });
  }

  remind(row: FollowUp): void {
    this.api.post(`/api/app/follow-ups/${row.horseId}/remind`, {}).subscribe({
      next: () => this.toast.success('Påminnelse köad.'),
      error: () => this.toast.error('Kunde inte skicka.')
    });
  }

  snooze(row: FollowUp): void {
    const weeks = this.snoozeWeeks[row.horseId] ?? 2;
    this.api.post(`/api/app/follow-ups/${row.horseId}/snooze`, { weeks }).subscribe({
      next: () => {
        this.rows.update(list => list.filter(x => x.horseId !== row.horseId));
        this.toast.success(`Snoozad ${weeks} veckor.`);
      }
    });
  }

  dismiss(row: FollowUp): void {
    const reason = this.reasons[row.horseId];
    this.api.post(`/api/app/follow-ups/${row.horseId}/dismiss`, { reason }).subscribe({
      next: () => {
        this.rows.update(list => list.filter(x => x.horseId !== row.horseId));
        this.toast.success('Avfärdad.');
      },
      error: () => this.toast.error('Ange en anledning.')
    });
  }
}
