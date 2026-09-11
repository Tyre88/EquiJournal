import { AfterViewInit, Component, ElementRef, OnDestroy, input, viewChild } from '@angular/core';
import { divIcon, latLngBounds, map as createMap, marker, polyline, tileLayer, type Map as LeafletMap } from 'leaflet';
import { SchemaHome, SchemaStop } from '../bookings/booking.models';

type Point = { lat: number; lon: number };

@Component({
  selector: 'app-schema-route-map',
  standalone: true,
  template: `
    <div
      #map
      class="schema-route-map"
      role="img"
      [attr.aria-label]="ariaLabel()"
    ></div>
  `,
  styles: [`
    :host { display: block; width: 100%; height: 100%; }
    .schema-route-map { width: 100%; height: 100%; }
  `]
})
export class SchemaRouteMapComponent implements AfterViewInit, OnDestroy {
  readonly home = input<SchemaHome | null>(null);
  readonly stops = input<SchemaStop[]>([]);
  private readonly mapEl = viewChild.required<ElementRef<HTMLDivElement>>('map');
  private map?: LeafletMap;
  private resize?: ResizeObserver;
  private coords: Point[] = [];
  private lastSize = '';

  ngAfterViewInit(): void {
    const el = this.mapEl().nativeElement;
    const stops = this.numberedStops();
    const home = this.point(this.home());
    this.coords = [
      ...(home ? [home] : []),
      ...stops.map(s => s.point)
    ];
    if (this.coords.length === 0) return;

    const map = createMap(el, {
      scrollWheelZoom: false,
      attributionControl: true
    });
    this.map = map;
    tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      maxZoom: 19
    }).addTo(map);

    const line = this.routeLine(home);
    if (line.length > 1) {
      polyline(line.map(p => [p.lat, p.lon] as [number, number]), {
        color: '#2d5016',
        weight: 4,
        opacity: 0.75,
        lineJoin: 'round'
      }).addTo(map);
    }

    if (home) {
      marker([home.lat, home.lon], {
        icon: this.pin('H', 'schema-route-pin schema-route-pin-home'),
        title: this.home()?.name || 'Hemmet',
        zIndexOffset: 200
      }).bindPopup(this.homePopup(), { autoPanPadding: [24, 48] }).addTo(map);
    }

    for (const group of this.groupedStops(stops)) {
      const label = this.numberLabel(group.numbers);
      marker([group.point.lat, group.point.lon], {
        icon: this.pin(label, 'schema-route-pin'),
        title: `Stopp ${label}`,
        zIndexOffset: 400 + group.numbers[0]
      }).bindPopup(group.popup, { autoPanPadding: [24, 48] }).addTo(map);
    }

    map.whenReady(() => this.syncView());
    this.resize = new ResizeObserver(() => this.syncView());
    this.resize.observe(el);
  }

  ngOnDestroy(): void {
    this.resize?.disconnect();
    this.map?.remove();
    this.map = undefined;
  }

  ariaLabel(): string {
    const count = this.stops().length;
    const stops = count === 1 ? '1 stopp' : `${count} stopp`;
    return `Dagens rutt med ${stops} numrerade på kartan`;
  }

  private syncView(): void {
    const map = this.map;
    if (!map || this.coords.length === 0) return;
    map.invalidateSize();
    const size = map.getSize();
    if (size.x < 80 || size.y < 80) return;
    const key = `${size.x}x${size.y}`;
    if (key === this.lastSize) return;
    this.lastSize = key;
    this.fit(map, this.coords);
  }

  private numberedStops(): { number: number; stop: SchemaStop; point: Point }[] {
    const list: { number: number; stop: SchemaStop; point: Point }[] = [];
    this.stops().forEach((stop, index) => {
      const point = this.point(stop);
      if (point) list.push({ number: index + 1, stop, point });
    });
    return list;
  }

  private groupedStops(stops: { number: number; stop: SchemaStop; point: Point }[]): {
    numbers: number[];
    point: Point;
    popup: string;
  }[] {
    const groups = new Map<string, { number: number; stop: SchemaStop; point: Point }[]>();
    for (const item of stops) {
      const key = `${item.point.lat.toFixed(4)},${item.point.lon.toFixed(4)}`;
      const group = groups.get(key) ?? [];
      group.push(item);
      groups.set(key, group);
    }
    return [...groups.values()].map(group => ({
      numbers: group.map(g => g.number),
      point: group[0].point,
      popup: group.map(g => this.stopPopup(g.number, g.stop)).join('<hr class="schema-route-popup-sep">')
    }));
  }

  private routeLine(home: Point | null): Point[] {
    const line: Point[] = [];
    if (home) line.push(home);
    for (const stop of this.stops()) {
      const point = this.point(stop);
      if (point) line.push(point);
    }
    return line;
  }

  private fit(map: LeafletMap, coords: Point[]): void {
    if (coords.length === 1) {
      map.setView([coords[0].lat, coords[0].lon], 12);
      return;
    }
    const bounds = latLngBounds(coords.map(p => [p.lat, p.lon] as [number, number]));
    if (bounds.getNorth() - bounds.getSouth() < 0.002 && bounds.getEast() - bounds.getWest() < 0.002) {
      map.setView(bounds.getCenter(), 13);
      return;
    }
    map.fitBounds(bounds, { padding: [48, 48], maxZoom: 13 });
  }

  private pin(label: string, className: string) {
    const width = Math.max(28, 16 + label.length * 8);
    return divIcon({
      className: 'schema-route-pin-wrap',
      html: `<span class="${className}">${this.escape(label)}</span>`,
      iconSize: [width, 28],
      iconAnchor: [width / 2, 28],
      popupAnchor: [0, -24]
    });
  }

  private homePopup(): string {
    const home = this.home();
    const title = this.escape(home?.name || 'Hemmet');
    const address = home?.address ? `<p>${this.escape(home.address)}</p>` : '';
    return `<div class="schema-route-popup"><strong>Start · ${title}</strong>${address}</div>`;
  }

  private stopPopup(number: number, stop: SchemaStop): string {
    const when = this.time(stop.startsAt);
    const who = [stop.ownerName, stop.horseName].filter(Boolean).join(' · ');
    const place = [stop.locationName, stop.addressStreet, [stop.addressPostcode, stop.addressCity].filter(Boolean).join(' ')]
      .map(p => p?.trim())
      .filter((p, i, all) => !!p && all.indexOf(p) === i)
      .join(' · ');
    return `<div class="schema-route-popup">
      <strong>Stopp ${number}</strong>
      <p>${this.escape(when)}${who ? ` · ${this.escape(who)}` : ''}</p>
      ${place ? `<p>${this.escape(place)}</p>` : ''}
    </div>`;
  }

  private numberLabel(numbers: number[]): string {
    if (numbers.length === 1) return String(numbers[0]);
    const sequential = numbers.every((n, i) => i === 0 || n === numbers[i - 1] + 1);
    return sequential ? `${numbers[0]}–${numbers[numbers.length - 1]}` : numbers.join(',');
  }

  private time(iso: string): string {
    const d = new Date(iso);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  private point(value?: { latitude?: number | null; longitude?: number | null } | null): Point | null {
    if (value?.latitude == null || value?.longitude == null) return null;
    return { lat: value.latitude, lon: value.longitude };
  }

  private escape(value: string): string {
    return value.replace(/[&<>"']/g, ch => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;'
    }[ch] ?? ch));
  }
}
