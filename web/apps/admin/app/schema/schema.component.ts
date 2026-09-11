import { Component, OnInit, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { EjEmptyStateComponent, EjIconComponent, EjPageHeaderComponent, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { ScheduleCacheService } from '../schedule-cache.service';
import { SchemaDay, SchemaHome, SchemaStop, TravelLeg } from '../bookings/booking.models';
import { statusClass, statusText } from '../bookings/status';
import { SchemaRouteMapComponent } from './schema-route-map.component';

@Component({
  selector: 'app-schema',
  standalone: true,
  imports: [RouterLink, EjPageHeaderComponent, EjEmptyStateComponent, EjIconComponent, SchemaRouteMapComponent],
  template: `
    <div class="page">
      <ej-page-header title="Dagens schema" subtitle="Dagens bokningar, ett tryck bort från journal.">
        <a routerLink="/bookings/new" class="btn-primary"><ej-icon name="plus" [size]="18" /> Ny bokning</a>
      </ej-page-header>

      @if (offline()) {
        <div class="alert alert-warning" role="status">Offline — visar sparad kopia av schemat.</div>
      }
      @if (error()) {
        <div class="alert alert-error">{{ error() }}</div>
      }

      @if (loading()) {
        <div class="skeleton skeleton-card"></div>
        <div class="skeleton skeleton-card"></div>
        <div class="skeleton skeleton-card"></div>
      } @else {
        <section class="card schema-home">
          <div class="schema-home-copy">
            <strong>Hemmet</strong>
            @if (home()?.address) {
              <p class="muted">{{ home()?.address }}</p>
            } @else {
              <p class="muted">Ingen hemadress. Körsträckor räknas från hemmet när adressen är ifylld.</p>
            }
          </div>
          <a routerLink="/settings/practice" class="btn-secondary">{{ home()?.address ? 'Ändra hemadress' : 'Ange hemadress' }}</a>
        </section>

        @if (rows().length === 0) {
        <ej-empty-state icon="calendar" title="Inga bokningar idag" description="När en tid bokas dyker den upp här.">
          <a routerLink="/bookings/new" class="btn-primary">Ny bokning</a>
        </ej-empty-state>
      } @else {
        @if (hasOverview()) {
          <section class="card schema-overview">
            <div class="schema-overview-copy">
              <strong>Dagens rutt</strong>
              <p class="muted">{{ routeSummary() }}</p>
            </div>
            <div class="schema-map">
              <app-schema-route-map [home]="home()" [stops]="rows()" />
            </div>
          </section>
        }

        <ul class="list">
          @for (row of rows(); track row.id; let i = $index) {
            <li class="card visit" [style.border-left-color]="row.treatmentColour || 'var(--color-primary)'">
              <a class="main" [routerLink]="['/bookings', row.id]">
                <div class="when">
                  <span class="stop-num" [attr.aria-label]="'Stopp ' + (i + 1)">{{ i + 1 }}</span>
                  <strong>{{ time(row.startsAt) }}–{{ time(row.endsAt) }}</strong>
                  <span class="badge" [class]="statusClass(row.status)">{{ statusText(row.status) }}</span>
                </div>
                <div class="who">
                  <strong>{{ row.ownerName }}</strong>
                  <span class="muted">{{ row.horseName }} · {{ row.treatmentName }}</span>
                  @if (address(row); as addr) {
                    <span class="muted">{{ addr }}</span>
                  }
                </div>
                @if (travelLabel(row.travel); as travel) {
                  <p class="travel-chip">
                    <ej-icon name="car" [size]="16" />
                    {{ travel }}
                  </p>
                }
                @if (row.clientNote) {
                  <p class="note">{{ row.clientNote }}</p>
                }
              </a>

              @if (embeds()[row.id]; as embed) {
                <div class="schema-map schema-map-stop">
                  <iframe
                    [src]="embed"
                    [title]="'Karta, ' + (row.locationName || row.ownerName || 'bokning')"
                    loading="lazy"
                    referrerpolicy="no-referrer-when-downgrade"
                  ></iframe>
                </div>
              }

              <div class="actions">
                @if (row.ownerPhone) {
                  <a class="btn-secondary" [href]="'tel:' + row.ownerPhone"><ej-icon name="phone" [size]="16" /> Ring</a>
                }
                @if (navigateUrl(row); as nav) {
                  <a class="btn-secondary" [href]="nav" target="_blank" rel="noopener">
                    <ej-icon name="map-pin" [size]="16" /> Navigera
                  </a>
                }
                @if (row.status === 'Confirmed') {
                  <button class="btn-primary" type="button" (click)="complete(row)" [disabled]="busy() === row.id">
                    Klar → journal
                  </button>
                }
              </div>
            </li>
          }
        </ul>
        }
      }
    </div>
  `,
  styles: [`
    .list { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--space-3); }
    .visit { border-left: 6px solid var(--color-primary); }
    .main { display: block; color: inherit; text-decoration: none; }
    .when { display: flex; align-items: center; gap: 0.5rem; font-size: 1.15rem; }
    .when strong { flex: 1; }
    .stop-num {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 1.6rem;
      height: 1.6rem;
      padding: 0 0.35rem;
      border-radius: 999px;
      background: var(--color-primary);
      color: var(--color-text-inverse);
      font-size: 0.8125rem;
      font-weight: 700;
      line-height: 1;
    }
    .who { display: flex; flex-direction: column; margin-top: 0.25rem; }
    .note { margin: 0.5rem 0 0; color: var(--color-text-muted); }
    .schema-home { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 0.75rem; }
    .schema-home-copy { flex: 1; min-width: 12rem; }
    .schema-home-copy p { margin: 0.25rem 0 0; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.85rem; }
    .actions .btn-primary { flex: 2; min-width: 160px; }
    .actions .btn-secondary { flex: 1; }
  `]
})
export class SchemaComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  private toast = inject(ToastService);
  private sanitizer = inject(DomSanitizer);
  private cache = inject(ScheduleCacheService);
  offline = signal(false);
  rows = signal<SchemaStop[]>([]);
  home = signal<SchemaHome | null>(null);
  embeds = signal<Record<string, SafeResourceUrl>>({});
  loading = signal(true);
  error = signal('');
  busy = signal<string | null>(null);
  statusClass = statusClass;
  statusText = statusText;

  ngOnInit(): void {
    this.loadDay(this.cache.todayAndTomorrow()[0]);
    if (navigator.onLine) {
      for (const date of this.cache.todayAndTomorrow()) {
        this.api.get<SchemaDay>('/api/app/schema/today', { date }).subscribe({
          next: day => void this.cache.put(date, day)
        });
      }
    }
  }

  private loadDay(date: string): void {
    this.loading.set(true);
    this.api.get<SchemaDay>('/api/app/schema/today', { date }).subscribe({
      next: day => {
        void this.cache.put(date, day);
        this.applyDay(day);
        this.offline.set(false);
        this.loading.set(false);
      },
      error: () => void this.cache.get(date).then(cached => {
        if (cached?.data) {
          this.applyDay(cached.data as SchemaDay);
          this.offline.set(true);
          this.error.set('');
        } else {
          this.error.set('Kunde inte ladda schemat.');
        }
        this.loading.set(false);
      })
    });
  }

  private applyDay(day: SchemaDay): void {
    this.home.set(day.home);
    this.rows.set(day.stops);
    const embeds: Record<string, SafeResourceUrl> = {};
    for (const stop of day.stops) {
      const url = this.buildStopUrl(stop, day);
      if (url) embeds[stop.id] = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    }
    this.embeds.set(embeds);
  }

  time(iso: string): string {
    const d = new Date(iso);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  address(row: SchemaStop): string | null {
    const parts = [row.locationName, row.addressStreet, [row.addressPostcode, row.addressCity].filter(Boolean).join(' ')]
      .map(p => p?.trim())
      .filter((p, i, all) => !!p && all.indexOf(p) === i);
    return parts.length ? parts.join(' · ') : null;
  }

  travelLabel(travel?: TravelLeg | null): string | null {
    if (!travel) return null;
    if (travel.samePlace) return 'Samma stall · ingen körning';
    const from = travel.from === 'home' ? 'Från hemmet' : `Från ${travel.fromLabel}`;
    if (travel.distanceKm != null && travel.durationMinutes != null) {
      const km = travel.distanceKm < 10 ? travel.distanceKm.toLocaleString('sv-SE', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) : Math.round(travel.distanceKm).toString();
      return `${from} · ${km} km · ${travel.durationMinutes} min med bil`;
    }
    return `${from} · körsträcka saknas`;
  }

  routeSummary(): string {
    const count = this.rows().length;
    const stops = count === 1 ? '1 stopp' : `${count} stopp`;
    const home = this.home()?.address;
    return home ? `${stops} från ${home}` : stops;
  }

  hasOverview(): boolean {
    return this.point(this.home()) != null || this.rows().some(row => this.point(row) != null);
  }

  private buildStopUrl(row: SchemaStop, day: SchemaDay): string | null {
    const dest = this.point(row);
    if (!dest) return null;
    const origin = this.originPointIn(row, day);
    return this.osmEmbed(origin ? [origin, dest] : [dest], dest);
  }

  navigateUrl(row: SchemaStop): string | null {
    const dest = this.point(row);
    if (!dest) {
      const addr = [row.addressStreet, row.addressPostcode, row.addressCity].filter(Boolean).join(', ');
      return addr ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(addr)}&travelmode=driving` : null;
    }
    const origin = this.originPointIn(row, { home: this.home(), stops: this.rows() });
    const destQ = `${dest.lat},${dest.lon}`;
    if (origin && !row.travel?.samePlace) {
      return `https://www.google.com/maps/dir/?api=1&origin=${origin.lat},${origin.lon}&destination=${destQ}&travelmode=driving`;
    }
    return `https://www.google.com/maps/dir/?api=1&destination=${destQ}&travelmode=driving`;
  }

  complete(row: SchemaStop): void {
    this.busy.set(row.id);
    this.api.post<{ journalId: string }>(`/api/app/bookings/${row.id}/complete`, {}).subscribe({
      next: res => this.router.navigate(['/journals', res.journalId, 'edit']),
      error: () => {
        this.toast.error('Kunde inte slutföra bokningen.');
        this.busy.set(null);
      }
    });
  }

  private originPointIn(row: SchemaStop, day: { home: SchemaHome | null; stops: SchemaStop[] }): { lat: number; lon: number } | null {
    if (row.travel?.samePlace) return this.point(row);
    if (row.travel?.from === 'previous') {
      const idx = day.stops.findIndex(r => r.id === row.id);
      for (let i = idx - 1; i >= 0; i--) {
        const prev = this.point(day.stops[i]);
        if (prev) return prev;
      }
    }
    return this.point(day.home);
  }

  private point(value?: { latitude?: number | null; longitude?: number | null } | null): { lat: number; lon: number } | null {
    if (value?.latitude == null || value?.longitude == null) return null;
    return { lat: value.latitude, lon: value.longitude };
  }

  private osmEmbed(points: { lat: number; lon: number }[], marker: { lat: number; lon: number }): string {
    const lats = points.map(p => p.lat);
    const lons = points.map(p => p.lon);
    const pad = points.length > 1 ? 0.04 : 0.025;
    const south = Math.min(...lats) - pad;
    const north = Math.max(...lats) + pad;
    const west = Math.min(...lons) - pad;
    const east = Math.max(...lons) + pad;
    return `https://www.openstreetmap.org/export/embed.html?bbox=${west}%2C${south}%2C${east}%2C${north}&layer=mapnik&marker=${marker.lat}%2C${marker.lon}`;
  }
}
