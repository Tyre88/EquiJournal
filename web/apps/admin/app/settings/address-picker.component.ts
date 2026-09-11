import { afterNextRender, Component, DestroyRef, effect, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { divIcon, map as createMap, marker as createMarker, tileLayer, type Map as LeafletMap, type Marker } from 'leaflet';
import { Api } from '../api';
import { GoogleMapsLoader } from './google-maps.loader';

export interface AddressPick {
  street: string;
  postcode: string;
  city: string;
  latitude: number;
  longitude: number;
}

interface PlaceHit {
  label: string;
  street: string;
  postcode: string;
  city: string;
  latitude: number;
  longitude: number;
}

type MapsPlace = {
  formatted_address?: string;
  geometry?: { location?: { lat(): number; lng(): number } };
  address_components?: { long_name: string; types: string[] }[];
};

@Component({
  selector: 'app-address-picker',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="field address-search">
      <label class="field-label" for="mapsSearch">Sök adress</label>
      <input
        #search
        id="mapsSearch"
        class="input"
        type="text"
        placeholder="Sök adress på kartan"
        autocomplete="off"
        [(ngModel)]="query"
        (ngModelChange)="onQuery($event)"
      />
      @if (hits().length) {
        <ul class="address-hits">
          @for (hit of hits(); track hit.label + hit.latitude) {
            <li>
              <button type="button" (click)="choose(hit)">{{ hit.label }}</button>
            </li>
          }
        </ul>
      }
    </div>
    <div #map class="address-map" role="img" aria-label="Karta för hemadress"></div>
    <p class="muted map-hint">Välj en adress i listan eller klicka på kartan. Latitud och longitud fylls i automatiskt.</p>
  `,
  styles: [`
    .address-search { position: relative; }
    .address-hits {
      list-style: none;
      margin: 0;
      padding: 0;
      position: absolute;
      left: 0;
      right: 0;
      z-index: 20;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 8px;
      max-height: 220px;
      overflow: auto;
      box-shadow: var(--shadow-sm);
    }
    .address-hits button {
      width: 100%;
      text-align: left;
      background: none;
      border: 0;
      padding: 0.65rem 0.8rem;
      cursor: pointer;
    }
    .address-hits button:hover { background: var(--color-primary-soft); }
    .address-map {
      width: 100%;
      height: 260px;
      border: 1px solid var(--color-border);
      border-radius: 8px;
      margin-bottom: 0.35rem;
    }
    .map-hint { margin: 0 0 1rem; }
  `]
})
export class AddressPickerComponent {
  readonly apiKey = input('');
  readonly latitude = input<number | null>(null);
  readonly longitude = input<number | null>(null);
  readonly picked = output<AddressPick>();

  query = '';
  readonly hits = signal<PlaceHit[]>([]);

  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');
  private readonly mapHost = viewChild<ElementRef<HTMLDivElement>>('map');
  private readonly api = inject(Api);
  private readonly googleLoader = inject(GoogleMapsLoader);
  private readonly destroy = inject(DestroyRef);
  private readonly viewReady = signal(false);
  private map?: LeafletMap;
  private pin?: Marker;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private googleReady = false;

  constructor() {
    afterNextRender(() => this.viewReady.set(true));
    effect(() => {
      if (this.viewReady()) this.ensureMap();
    });
    effect(() => {
      const key = this.apiKey();
      if (this.viewReady() && key) void this.bindGoogle(key);
    });
    effect(() => {
      const lat = this.latitude();
      const lon = this.longitude();
      if (lat != null && lon != null) this.movePin(lat, lon, false);
    });
    this.destroy.onDestroy(() => {
      if (this.searchTimer) clearTimeout(this.searchTimer);
      this.map?.remove();
    });
  }

  onQuery(value: string): void {
    if (this.googleReady) return;
    if (this.searchTimer) clearTimeout(this.searchTimer);
    if (!value || value.trim().length < 3) {
      this.hits.set([]);
      return;
    }
    this.searchTimer = setTimeout(() => {
      this.api.get<PlaceHit[]>('/api/app/places', { q: value.trim() }).subscribe({
        next: rows => this.hits.set(rows ?? []),
        error: () => this.hits.set([])
      });
    }, 250);
  }

  choose(hit: PlaceHit): void {
    this.hits.set([]);
    this.query = hit.label;
    this.apply({
      street: hit.street || hit.label,
      postcode: hit.postcode,
      city: hit.city,
      latitude: hit.latitude,
      longitude: hit.longitude
    });
  }

  private ensureMap(): void {
    const host = this.mapHost()?.nativeElement;
    if (!host || this.map) return;
    const start = this.center();
    this.map = createMap(host, { zoomControl: true, attributionControl: true }).setView([start.lat, start.lng], start.lat === 55.99 ? 8 : 16);
    tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap'
    }).addTo(this.map);
    this.pin = createMarker([start.lat, start.lng], {
      draggable: true,
      icon: divIcon({
        className: 'schema-route-pin schema-route-pin-home',
        html: '<span>H</span>',
        iconSize: [28, 28],
        iconAnchor: [14, 28]
      })
    }).addTo(this.map);
    this.map.on('click', e => this.reverse(e.latlng.lat, e.latlng.lng));
    this.pin.on('dragend', () => {
      const pos = this.pin?.getLatLng();
      if (pos) this.reverse(pos.lat, pos.lng);
    });
    setTimeout(() => this.map?.invalidateSize(), 50);
  }

  private async bindGoogle(key: string): Promise<void> {
    const input = this.search()?.nativeElement;
    if (!input || this.googleReady) return;
    const ok = await this.googleLoader.load(key);
    const maps = (window as unknown as {
      google?: {
        maps?: {
          places?: {
            Autocomplete: new (el: HTMLInputElement, opts: Record<string, unknown>) => {
              addListener(name: string, handler: () => void): void;
              getPlace(): MapsPlace;
            };
          };
        };
      };
    }).google?.maps;
    if (!ok || !maps?.places) return;
    const autocomplete = new maps.places.Autocomplete(input, {
      componentRestrictions: { country: 'se' },
      fields: ['address_components', 'geometry', 'formatted_address'],
      types: ['geocode']
    });
    autocomplete.addListener('place_changed', () => {
      const pick = this.fromGoogle(autocomplete.getPlace());
      if (pick) {
        this.hits.set([]);
        this.apply(pick);
      }
    });
    this.googleReady = true;
  }

  private reverse(lat: number, lon: number): void {
    this.api.get<PlaceHit>('/api/app/places/reverse', { lat, lon }).subscribe({
      next: hit => this.choose(hit),
      error: () => this.apply({ street: '', postcode: '', city: '', latitude: lat, longitude: lon })
    });
  }

  private apply(pick: AddressPick): void {
    this.movePin(pick.latitude, pick.longitude, true);
    this.picked.emit(pick);
  }

  private movePin(lat: number, lon: number, zoom: boolean): void {
    if (!this.map || !this.pin) return;
    this.pin.setLatLng([lat, lon]);
    if (zoom) this.map.setView([lat, lon], 16);
    else this.map.panTo([lat, lon]);
  }

  private fromGoogle(place: MapsPlace): AddressPick | null {
    const loc = place.geometry?.location;
    if (!loc) return null;
    const get = (...types: string[]) =>
      place.address_components?.find(c => types.some(t => c.types.includes(t)))?.long_name ?? '';
    const street = [get('route'), get('street_number')].filter(Boolean).join(' ');
    return {
      street: street || place.formatted_address || '',
      postcode: get('postal_code'),
      city: get('postal_town', 'locality', 'administrative_area_level_2'),
      latitude: Number(loc.lat().toFixed(7)),
      longitude: Number(loc.lng().toFixed(7))
    };
  }

  private center(): { lat: number; lng: number } {
    const lat = this.latitude();
    const lon = this.longitude();
    if (lat != null && lon != null) return { lat, lng: lon };
    return { lat: 55.99, lng: 13.72 };
  }
}
