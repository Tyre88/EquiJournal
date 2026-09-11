import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Api } from '../api';

interface HistoryRow {
  id: string;
  actorId?: string;
  action: string;
  timestamp: string;
}

@Component({
  selector: 'app-entity-history',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="entity-history">
      <button type="button" class="btn-ghost" (click)="toggle()" [attr.aria-expanded]="open()">
        Visa historik
      </button>
      @if (open()) {
        <ul class="history-list">
          @for (row of items(); track row.id) {
            <li>
              <time>{{ row.timestamp | date:'yyyy-MM-dd HH:mm' }}</time>
              <span>{{ row.action }}</span>
              @if (row.actorId) { <span class="muted">· {{ row.actorId }}</span> }
            </li>
          } @empty {
            <li class="muted">Ingen historik.</li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .entity-history { margin-top: 0.75rem; }
    .history-list { list-style: none; padding: 0; margin: 0.5rem 0 0; font-size: var(--text-sm); }
    .history-list li { padding: 0.35rem 0; border-bottom: 1px solid var(--color-border); }
  `]
})
export class EntityHistoryComponent implements OnInit {
  @Input({ required: true }) entityType!: string;
  @Input({ required: true }) entityId!: string;

  private api = inject(Api);
  open = signal(false);
  items = signal<HistoryRow[]>([]);

  ngOnInit(): void {}

  toggle(): void {
    this.open.update(v => !v);
    if (this.open() && this.items().length === 0) {
      this.api.get<HistoryRow[]>(`/api/app/audit/entity/${this.entityType}/${this.entityId}`)
        .subscribe(rows => this.items.set(rows));
    }
  }
}
