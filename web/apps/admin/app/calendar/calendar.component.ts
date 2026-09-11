import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EjIconComponent, EjPageHeaderComponent, EjSpinnerComponent } from '@equijournal/ui';
import { Api } from '../api';
import { BookingListItem } from '../bookings/booking.models';

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjIconComponent, EjSpinnerComponent],
  template: `
    <div class="page page--wide">
      <ej-page-header title="Kalender">
        <div class="toolbar" style="margin:0">
          <button type="button" class="btn-secondary" (click)="shift(-1)" aria-label="Föregående">
            <ej-icon name="chevron-left" />
          </button>
          <strong>{{ label() }}</strong>
          <button type="button" class="btn-secondary" (click)="shift(1)" aria-label="Nästa">
            <ej-icon name="chevron-right" />
          </button>
          <div class="seg" role="group" aria-label="Vy">
            <button type="button" [class.active]="view() === 'week'" (click)="setView('week')">Vecka</button>
            <button type="button" [class.active]="view() === 'month'" (click)="setView('month')">Månad</button>
          </div>
          <a routerLink="/bookings/new" class="btn-primary"><ej-icon name="plus" [size]="16" /> Ny bokning</a>
        </div>
      </ej-page-header>

      @if (error()) { <div class="alert alert-error">{{ error() }}</div> }
      @if (loading()) { <p class="loading"><ej-spinner /> Laddar kalender…</p> }

      @if (view() === 'week') {
        <div class="week">
          @for (day of weekDays(); track day.key) {
            <section class="card day" [class.today]="day.isToday">
              <h2>{{ day.label }}</h2>
              <button class="add" type="button" (click)="createAt(day.date, 8)">+ tid</button>
              @for (row of day.rows; track row.id) {
                <article
                  class="evt"
                  [style.background]="row.treatmentColour || 'var(--color-primary)'"
                  draggable="true"
                  (dragstart)="dragStart(row)"
                  (click)="open(row.id)">
                  <span>{{ time(row.startsAt) }} {{ row.horseName }}</span>
                </article>
              }
              <div class="drop" (dragover)="$event.preventDefault()" (drop)="dropOn(day.date)"></div>
            </section>
          }
        </div>
      } @else {
        <div class="month">
          @for (cell of monthCells(); track cell.key) {
            <div class="cell card" [class.out]="!cell.inMonth" [class.today]="cell.isToday">
              <button type="button" class="daynum" (click)="createAt(cell.date, 8)">{{ cell.date.getDate() }}</button>
              @for (row of cell.rows; track row.id) {
                <button type="button" class="chip" [style.background]="row.treatmentColour || 'var(--color-primary)'" (click)="open(row.id)">
                  {{ row.horseName }}
                </button>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .week { display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.5rem; }
    .day { min-height: 200px; padding: 0.6rem; }
    .day h2 { font-size: 0.8rem; margin: 0 0 0.4rem; }
    .day.today, .cell.today { outline: 2px solid var(--color-primary); }
    .evt, .chip {
      color: #fff; border: 0; border-radius: 6px; padding: 0.35rem; margin-bottom: 0.3rem;
      cursor: pointer; font-size: 0.8rem; width: 100%; text-align: left;
    }
    .add, .drop { width: 100%; min-height: 36px; border: 0; background: var(--color-surface-muted); border-radius: 6px; cursor: pointer; color: var(--color-text-muted); }
    .month { display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.35rem; }
    .cell { min-height: 92px; padding: 0.35rem; }
    .cell.out { opacity: 0.45; }
    .daynum { border: 0; background: none; font-weight: 700; min-height: 28px; padding: 0; cursor: pointer; }
    @media (max-width: 800px) { .week { grid-template-columns: 1fr; } .month { grid-template-columns: repeat(2, 1fr); } }
  `]
})
export class CalendarComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  view = signal<'week' | 'month'>('week');
  cursor = signal(startOfWeek(new Date()));
  rows = signal<BookingListItem[]>([]);
  error = signal('');
  loading = signal(false);
  dragging: BookingListItem | null = null;

  ngOnInit(): void {
    this.load();
  }

  label(): string {
    const d = this.cursor();
    return d.toLocaleDateString('sv-SE', { month: 'long', year: 'numeric' });
  }

  setView(next: 'week' | 'month'): void {
    this.view.set(next);
    this.load();
  }

  weekDays() {
    return Array.from({ length: 7 }, (_, i) => {
      const date = addDays(this.cursor(), i);
      return {
        key: date.toISOString(),
        date,
        isToday: sameDay(date, new Date()),
        label: date.toLocaleDateString('sv-SE', { weekday: 'short', day: 'numeric' }),
        rows: this.rowsFor(date)
      };
    });
  }

  monthCells() {
    const first = new Date(this.cursor().getFullYear(), this.cursor().getMonth(), 1);
    const start = startOfWeek(first);
    return Array.from({ length: 42 }, (_, i) => {
      const date = addDays(start, i);
      return {
        key: date.toISOString(),
        date,
        isToday: sameDay(date, new Date()),
        inMonth: date.getMonth() === first.getMonth(),
        rows: this.rowsFor(date)
      };
    });
  }

  shift(delta: number): void {
    const d = new Date(this.cursor());
    if (this.view() === 'week') d.setDate(d.getDate() + delta * 7);
    else d.setMonth(d.getMonth() + delta);
    this.cursor.set(this.view() === 'week' ? startOfWeek(d) : new Date(d.getFullYear(), d.getMonth(), 1));
    this.load();
  }

  load(): void {
    const from = this.view() === 'week' ? this.cursor() : startOfWeek(new Date(this.cursor().getFullYear(), this.cursor().getMonth(), 1));
    const to = addDays(from, this.view() === 'week' ? 7 : 42);
    this.loading.set(true);
    this.api.get<BookingListItem[]>('/api/app/bookings', {
      from: from.toISOString(),
      to: to.toISOString()
    }).subscribe({
      next: rows => {
        this.rows.set(rows.filter(r => r.status === 'Requested' || r.status === 'Confirmed'));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Kunde inte ladda kalendern.');
        this.loading.set(false);
      }
    });
  }

  time(iso: string): string {
    const d = new Date(iso);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  open(id: string): void {
    this.router.navigate(['/bookings', id]);
  }

  createAt(date: Date, hour: number): void {
    const d = new Date(date);
    d.setHours(hour, 0, 0, 0);
    this.router.navigate(['/bookings/new'], { queryParams: { startsAt: d.toISOString() } });
  }

  dragStart(row: BookingListItem): void {
    this.dragging = row;
  }

  dropOn(date: Date): void {
    const row = this.dragging;
    this.dragging = null;
    if (!row) return;
    const original = new Date(row.visitStartsAt);
    const next = new Date(date);
    next.setHours(original.getHours(), original.getMinutes(), 0, 0);
    this.api.patch(`/api/app/visits/${row.visitId}`, { startsAt: next.toISOString() }).subscribe({
      next: () => this.load(),
      error: err => {
        this.error.set(err?.error?.message || 'Tiden är inte ledig.');
        this.load();
      }
    });
  }

  private rowsFor(date: Date): BookingListItem[] {
    return this.rows().filter(r => sameDay(new Date(r.startsAt), date));
  }
}

function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay() || 7;
  d.setDate(d.getDate() - day + 1);
  d.setHours(0, 0, 0, 0);
  return d;
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}
