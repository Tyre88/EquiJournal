import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';

interface SearchResults {
  clients: { id: string; name: string; type: string }[];
  horses: { id: string; name: string; ownerName: string; type: string }[];
  journals: { id: string; horseName: string; ownerName: string; type: string }[];
  bookings: { id: string; horseName: string; ownerName: string; treatmentName: string; type: string }[];
}

const RECENT_KEY = 'equine:recent-searches';
const RECENT_ITEMS_KEY = 'equine:recent-items';

@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="search-backdrop" (click)="close()" role="presentation">
        <div class="search-panel" (click)="$event.stopPropagation()" role="dialog" aria-label="Sök">
          <input
            #q
            class="input search-input"
            type="search"
            [(ngModel)]="query"
            (input)="onQuery()"
            placeholder="Sök kunder, hästar, journaler, bokningar…"
            autocomplete="off"
            aria-label="Global sökning"
          />
          @if (!query && recentSearches().length) {
            <p class="muted section-label">Senaste sökningar</p>
            <ul class="search-results">
              @for (s of recentSearches(); track s) {
                <li><button type="button" (click)="query = s; onQuery()">{{ s }}</button></li>
              }
            </ul>
          }
          @if (results(); as r) {
            @if (r.clients.length) {
              <p class="section-label">Kunder</p>
              <ul class="search-results">
                @for (c of r.clients; track c.id) {
                  <li><button type="button" (click)="go('/owners/' + c.id, c.name)">{{ c.name }}</button></li>
                }
              </ul>
            }
            @if (r.horses.length) {
              <p class="section-label">Hästar</p>
              <ul class="search-results">
                @for (h of r.horses; track h.id) {
                  <li><button type="button" (click)="go('/horses/' + h.id, h.name)">{{ h.name }} · {{ h.ownerName }}</button></li>
                }
              </ul>
            }
            @if (r.journals.length) {
              <p class="section-label">Journaler</p>
              <ul class="search-results">
                @for (j of r.journals; track j.id) {
                  <li><button type="button" (click)="go('/journals/' + j.id, j.horseName)">{{ j.horseName }} · {{ j.ownerName }}</button></li>
                }
              </ul>
            }
            @if (r.bookings.length) {
              <p class="section-label">Bokningar</p>
              <ul class="search-results">
                @for (b of r.bookings; track b.id) {
                  <li><button type="button" (click)="go('/bookings/' + b.id, b.treatmentName)">{{ b.treatmentName }} · {{ b.horseName }}</button></li>
                }
              </ul>
            }
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .search-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.4); z-index: 200; display: flex; padding: 1rem; justify-content: center; align-items: flex-start; }
    .search-panel { background: var(--color-surface); border-radius: var(--radius-lg); padding: 1rem; width: min(100%, 32rem); max-height: 80vh; overflow: auto; box-shadow: var(--shadow-md); }
    .search-input { width: 100%; min-height: var(--tap-min); font-size: 1rem; }
    .section-label { margin: 0.75rem 0 0.25rem; font-size: var(--text-sm); font-weight: 600; }
    .search-results { list-style: none; padding: 0; margin: 0; }
    .search-results button { width: 100%; text-align: left; min-height: var(--tap-min); padding: 0.5rem; border: none; background: none; cursor: pointer; border-radius: var(--radius-sm); }
    .search-results button:hover, .search-results button:focus-visible { background: var(--color-primary-soft); outline: none; box-shadow: var(--focus-ring); }
  `]
})
export class GlobalSearchComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  open = signal(false);
  query = '';
  results = signal<SearchResults | null>(null);
  recentSearches = signal<string[]>([]);

  ngOnInit(): void {
    try {
      this.recentSearches.set(JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]'));
    } catch { /* ignore */ }
  }

  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      this.open.set(true);
    }
    if (e.key === 'Escape') this.close();
  }

  show(): void { this.open.set(true); }
  close(): void { this.open.set(false); this.query = ''; this.results.set(null); }

  onQuery(): void {
    const q = this.query.trim();
    if (q.length < 2) { this.results.set(null); return; }
    this.api.get<SearchResults>('/api/app/search', { q, limit: 8 }).subscribe(r => this.results.set(r));
  }

  go(path: string, label: string): void {
    this.saveSearch(this.query);
    this.saveRecentItem(path, label);
    this.close();
    this.router.navigateByUrl(path);
  }

  private saveSearch(q: string): void {
    if (!q.trim()) return;
    const list = [q, ...this.recentSearches().filter(s => s !== q)].slice(0, 8);
    this.recentSearches.set(list);
    localStorage.setItem(RECENT_KEY, JSON.stringify(list));
  }

  private saveRecentItem(path: string, label: string): void {
    try {
      const items = JSON.parse(localStorage.getItem(RECENT_ITEMS_KEY) ?? '[]') as { path: string; label: string }[];
      const next = [{ path, label }, ...items.filter(i => i.path !== path)].slice(0, 10);
      localStorage.setItem(RECENT_ITEMS_KEY, JSON.stringify(next));
    } catch { /* ignore */ }
  }
}
