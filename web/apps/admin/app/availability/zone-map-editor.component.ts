import { afterNextRender, Component, DestroyRef, effect, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  circle as createCircle,
  geoJSON,
  map as createMap,
  tileLayer,
  type Circle,
  type Layer,
  type Map as LeafletMap,
  type PathOptions
} from 'leaflet';
import '@geoman-io/leaflet-geoman-free';
import { ConfirmService, ToastService } from '@equijournal/ui';
import { Api } from '../api';
import { GeoJsonPolygon, Zone, ZoneCenter } from '../bookings/booking.models';

const COLORS = ['#2d5016', '#175cd3', '#9a6700', '#b42318', '#0e7490', '#7c3aed', '#c2410c', '#1b6b2a'];
const NEW_ID = '__new';

interface ZoneRequest {
  name: string;
  geometryKind?: 'polygon' | 'circle' | null;
  geometry?: GeoJsonPolygon | null;
  center?: ZoneCenter | null;
  radiusKm?: number | null;
  bufferKm: number;
  travelBufferMinutes: number;
}

type GeomanMap = LeafletMap & {
  pm: {
    setLang(lang: string, texts?: Record<string, string>, fallback?: string): void;
    enableDraw(shape: 'Polygon' | 'Circle'): void;
    disableDraw(): void;
    addControls(opts: Record<string, unknown>): void;
  };
};

type GeomanLayer = Layer & {
  pm?: { enable(): void; disable(): void };
  toGeoJSON(): { geometry?: GeoJsonPolygon };
};

@Component({
  selector: 'app-zone-map-editor',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="zone-layout">
      <div class="zone-list">
        @for (z of zones(); track z.id; let i = $index) {
          <button
            type="button"
            class="zone-item"
            [class.active]="selectedId() === z.id"
            (click)="select(z.id)"
          >
            <span class="swatch" [style.background]="color(i)"></span>
            <span class="zone-meta">
              <strong>{{ z.name }}</strong>
              <small>{{ kindLabel(z) }}</small>
            </span>
          </button>
        }
        <button type="button" class="btn-secondary" (click)="startNew()">Ny zon</button>
      </div>

      <div class="zone-map-wrap">
        <div #map class="zone-map" role="img" aria-label="Karta för zoner"></div>
        <div class="draw-bar">
          <button type="button" class="btn-secondary btn-sm" [disabled]="!canDraw()" (click)="drawPolygon()">Rita yta</button>
          <button type="button" class="btn-secondary btn-sm" [disabled]="!canDraw()" (click)="drawCircle()">Rita cirkel</button>
          @if (draftKind()) {
            <button type="button" class="btn-ghost btn-sm" (click)="clearDraftShape()">Rensa yta</button>
          }
        </div>
        <p class="muted hint">Rita en yta eller cirkel. Bufferten i kilometer utökar täckningen utanför kanten.</p>
      </div>

      <div class="zone-form">
        @if (selectedId()) {
          <div class="field">
            <label class="field-label" for="zoneName">Namn</label>
            <input id="zoneName" class="input" [(ngModel)]="draftName" [disabled]="isFallback()" />
          </div>
          <div class="field">
            <label class="field-label" for="zoneBuffer">Buffert (km)</label>
            <input id="zoneBuffer" class="input" type="number" min="0" step="0.5" [(ngModel)]="draftBufferKm" [disabled]="isFallback()" (ngModelChange)="refreshHalo()" />
          </div>
          <div class="field">
            <label class="field-label" for="zoneTravel">Restid (min)</label>
            <input id="zoneTravel" class="input" type="number" min="0" step="5" [(ngModel)]="draftTravel" />
          </div>
          @if (draftKind() === 'circle' && draftRadiusKm != null) {
            <p class="muted">Radie {{ draftRadiusKm.toFixed(1) }} km</p>
          }
          <div class="form-actions">
            <button type="button" class="btn-primary" (click)="save()">Spara</button>
            @if (selectedId() && selectedId() !== newId && !isFallback()) {
              <button type="button" class="btn-danger" (click)="remove()">Ta bort</button>
            }
          </div>
        } @else {
          <p class="muted">Välj en zon eller skapa en ny och rita täckningen på kartan.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .zone-layout { display: grid; grid-template-columns: 200px 1fr 220px; gap: 1rem; align-items: start; }
    .zone-list { display: flex; flex-direction: column; gap: 0.4rem; }
    .zone-item {
      display: flex; gap: 0.5rem; align-items: center; text-align: left;
      border: 1px solid var(--color-border); background: var(--color-surface);
      border-radius: 8px; padding: 0.55rem 0.65rem; cursor: pointer; color: inherit;
    }
    .zone-item.active { border-color: var(--color-primary); background: var(--color-primary-soft); }
    .swatch { width: 12px; height: 12px; border-radius: 50%; flex: 0 0 12px; }
    .zone-meta { display: flex; flex-direction: column; min-width: 0; }
    .zone-meta small { color: var(--color-text-muted); font-size: 0.75rem; }
    .zone-map { height: 420px; border: 1px solid var(--color-border); border-radius: 8px; }
    .draw-bar { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.5rem; }
    .hint { margin: 0.4rem 0 0; }
    :host ::ng-deep .leaflet-pm-toolbar { display: none; }
    .form-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    @media (max-width: 900px) {
      .zone-layout { grid-template-columns: 1fr; }
      .zone-map { height: 320px; }
    }
  `]
})
export class ZoneMapEditorComponent {
  readonly zones = input<Zone[]>([]);
  readonly homeLat = input<number | null>(null);
  readonly homeLng = input<number | null>(null);
  readonly changed = output<void>();

  readonly newId = NEW_ID;
  readonly selectedId = signal<string | null>(null);

  draftName = '';
  draftBufferKm = 0;
  draftTravel = 0;
  draftKind = signal<'polygon' | 'circle' | null>(null);
  draftGeometry: GeoJsonPolygon | null = null;
  draftCenter: ZoneCenter | null = null;
  draftRadiusKm: number | null = null;

  private readonly mapHost = viewChild<ElementRef<HTMLDivElement>>('map');
  private readonly api = inject(Api);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly destroy = inject(DestroyRef);
  private readonly viewReady = signal(false);
  private map?: GeomanMap;
  private layers = new Map<string, Layer[]>();
  private draftLayer?: GeomanLayer;
  private haloLayer?: Layer;

  constructor() {
    afterNextRender(() => this.viewReady.set(true));
    effect(() => {
      if (this.viewReady()) this.ensureMap();
    });
    effect(() => {
      this.zones();
      if (this.map) this.syncFromInput();
    });
    this.destroy.onDestroy(() => this.map?.remove());
  }

  private syncFromInput(): void {
    this.redrawAll();
    const current = this.selectedId();
    const zones = this.zones();
    if (current === NEW_ID) return;
    if (current && zones.some(z => z.id === current))
      this.select(current);
    else if (current)
      this.selectedId.set(null);
  }

  color(index: number): string {
    return COLORS[index % COLORS.length];
  }

  kindLabel(z: Zone): string {
    if (z.isFallback) return 'Övriga adresser';
    if (z.geometryKind === 'circle') return `Cirkel${z.bufferKm ? ` + ${z.bufferKm} km` : ''}`;
    if (z.geometryKind === 'polygon') return `Yta${z.bufferKm ? ` + ${z.bufferKm} km` : ''}`;
    return 'Ingen yta ännu';
  }

  canDraw(): boolean {
    return !!this.selectedId() && !this.isFallback();
  }

  isFallback(): boolean {
    const id = this.selectedId();
    return !!this.zones().find(z => z.id === id)?.isFallback;
  }

  select(id: string): void {
    this.clearDraftLayer();
    this.selectedId.set(id);
    const zone = this.zones().find(z => z.id === id);
    if (!zone) return;
    this.draftName = zone.name;
    this.draftBufferKm = zone.bufferKm ?? 0;
    this.draftTravel = zone.travelBufferMinutes ?? 0;
    this.draftKind.set(zone.geometryKind ?? null);
    this.draftGeometry = zone.geometry ?? null;
    this.draftCenter = zone.center ?? null;
    this.draftRadiusKm = zone.radiusKm ?? null;
    this.fitSelected(zone);
    this.enableEditOn(zone.id);
    this.refreshHalo();
  }

  startNew(): void {
    this.clearDraftLayer();
    this.selectedId.set(NEW_ID);
    this.draftName = '';
    this.draftBufferKm = 0;
    this.draftTravel = 0;
    this.draftKind.set(null);
    this.draftGeometry = null;
    this.draftCenter = null;
    this.draftRadiusKm = null;
    this.refreshHalo();
  }

  drawPolygon(): void {
    if (!this.map || !this.canDraw()) return;
    this.map.pm.disableDraw();
    this.map.pm.enableDraw('Polygon');
  }

  drawCircle(): void {
    if (!this.map || !this.canDraw()) return;
    this.map.pm.disableDraw();
    this.map.pm.enableDraw('Circle');
  }

  clearDraftShape(): void {
    this.clearDraftLayer();
    this.draftKind.set(null);
    this.draftGeometry = null;
    this.draftCenter = null;
    this.draftRadiusKm = null;
    this.refreshHalo();
  }

  refreshHalo(): void {
    this.haloLayer?.remove();
    this.haloLayer = undefined;
    if (!this.map || this.isFallback()) return;
    const color = this.selectedColor();
    const style: PathOptions = { color, weight: 1, fillColor: color, fillOpacity: 0.08, dashArray: '4 4' };
    if (this.draftKind() === 'circle' && this.draftCenter && this.draftRadiusKm != null) {
      const meters = (this.draftRadiusKm + Math.max(0, Number(this.draftBufferKm) || 0)) * 1000;
      this.haloLayer = createCircle([this.draftCenter.lat, this.draftCenter.lng], { radius: meters, ...style }).addTo(this.map);
      return;
    }
    const zone = this.zones().find(z => z.id === this.selectedId());
    if (this.draftKind() === 'polygon' && zone?.effectiveGeometry && (Number(this.draftBufferKm) || 0) === (zone.bufferKm ?? 0)) {
      this.haloLayer = geoJSON(zone.effectiveGeometry as never, { style }).addTo(this.map);
    }
  }

  save(): void {
    const id = this.selectedId();
    if (!id) return;
    if (!this.draftName.trim()) {
      this.toast.error('Ange ett namn.');
      return;
    }
    this.syncDraftFromLayer();
    const body = this.toRequest();
    if (id === NEW_ID) {
      this.api.post<Zone>('/api/app/zones', body).subscribe({
        next: created => {
          this.toast.success('Zonen skapades.');
          this.changed.emit();
          this.selectedId.set(created.id);
        },
        error: () => this.toast.error('Kunde inte skapa zonen.')
      });
      return;
    }
    this.api.put(`/api/app/zones/${id}`, body).subscribe({
      next: () => {
        this.toast.success('Zonen sparades.');
        this.changed.emit();
      },
      error: () => this.toast.error('Kunde inte spara zonen.')
    });
  }

  async remove(): Promise<void> {
    const id = this.selectedId();
    if (!id || id === NEW_ID) return;
    const ok = await this.confirm.confirm({
      title: 'Ta bort zon',
      message: 'Zonen tas bort permanent.',
      confirmLabel: 'Ta bort',
      destructive: true
    });
    if (!ok) return;
    this.api.delete(`/api/app/zones/${id}`).subscribe({
      next: () => {
        this.toast.success('Zonen togs bort.');
        this.selectedId.set(null);
        this.changed.emit();
      },
      error: () => this.toast.error('Kunde inte ta bort zonen.')
    });
  }

  private ensureMap(): void {
    const host = this.mapHost()?.nativeElement;
    if (!host || this.map) return;
    const start = this.center();
    const map = createMap(host, { zoomControl: true, attributionControl: true })
      .setView([start.lat, start.lng], start.lat === 55.63 ? 8 : 11) as GeomanMap;
    tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap'
    }).addTo(map);
    map.on('pm:create', e => this.onCreated(e as unknown as { shape: string; layer: GeomanLayer }));
    this.map = map;
    setTimeout(() => map.invalidateSize(), 50);
    this.redrawAll();
  }

  private onCreated(e: { shape: string; layer: GeomanLayer }): void {
    this.clearDraftLayer();
    this.draftLayer = e.layer;
    this.draftLayer.pm?.enable();
    this.draftLayer.on('pm:change', () => this.syncDraftFromLayer());
    this.syncDraftFromLayer();
    this.map?.pm.disableDraw();
    this.refreshHalo();
  }

  private syncDraftFromLayer(): void {
    const layer = this.draftLayer as (GeomanLayer & Circle) | undefined;
    if (!layer) return;
    if ('getRadius' in layer && typeof layer.getRadius === 'function') {
      const pos = layer.getLatLng();
      this.draftKind.set('circle');
      this.draftCenter = { lat: pos.lat, lng: pos.lng };
      this.draftRadiusKm = layer.getRadius() / 1000;
      this.draftGeometry = null;
      return;
    }
    const raw = layer.toGeoJSON() as { geometry?: GeoJsonPolygon; features?: Array<{ geometry?: GeoJsonPolygon }> };
    const geo = raw.geometry ?? raw.features?.[0]?.geometry;
    if (geo?.type === 'Polygon') {
      this.draftKind.set('polygon');
      this.draftGeometry = geo;
      this.draftCenter = null;
      this.draftRadiusKm = null;
    }
  }

  private toRequest(): ZoneRequest {
    return {
      name: this.draftName.trim(),
      geometryKind: this.draftKind(),
      geometry: this.draftKind() === 'polygon' ? this.draftGeometry : null,
      center: this.draftKind() === 'circle' ? this.draftCenter : null,
      radiusKm: this.draftKind() === 'circle' ? this.draftRadiusKm : null,
      bufferKm: Number(this.draftBufferKm) || 0,
      travelBufferMinutes: Number(this.draftTravel) || 0
    };
  }

  private redrawAll(): void {
    if (!this.map) return;
    for (const group of this.layers.values())
      for (const layer of group) layer.remove();
    this.layers.clear();
    this.zones().forEach((zone, i) => this.paintZone(zone, this.color(i)));
  }

  private paintZone(zone: Zone, color: string): void {
    if (!this.map || zone.isFallback) return;
    const layers: Layer[] = [];
    const core: PathOptions = { color, weight: 2, fillColor: color, fillOpacity: 0.22 };
    if (zone.geometryKind === 'circle' && zone.center && zone.radiusKm != null) {
      layers.push(createCircle([zone.center.lat, zone.center.lng], { radius: zone.radiusKm * 1000, ...core }).addTo(this.map));
    } else if (zone.geometry && zone.geometryKind === 'polygon') {
      layers.push(geoJSON(zone.geometry as never, { style: core }).addTo(this.map));
    }
    this.layers.set(zone.id, layers);
  }

  private fitSelected(zone: Zone): void {
    if (!this.map) return;
    if (zone.geometryKind === 'circle' && zone.center && zone.radiusKm != null) {
      const c = createCircle([zone.center.lat, zone.center.lng], { radius: (zone.radiusKm + (zone.bufferKm || 0)) * 1000 });
      this.map.fitBounds(c.getBounds(), { padding: [24, 24], maxZoom: 12 });
      return;
    }
    if (zone.effectiveGeometry || zone.geometry) {
      const layer = geoJSON((zone.effectiveGeometry ?? zone.geometry) as never);
      const bounds = layer.getBounds();
      if (bounds.isValid()) this.map.fitBounds(bounds, { padding: [24, 24], maxZoom: 12 });
    }
  }

  private enableEditOn(id: string): void {
    for (const group of this.layers.values()) {
      for (const layer of group)
        (layer as GeomanLayer).pm?.disable();
    }
    const layer = this.layers.get(id)?.[0] as GeomanLayer | undefined;
    if (!layer) return;
    layer.pm?.enable();
    this.draftLayer = layer;
    layer.on('pm:change', () => this.syncDraftFromLayer());
    this.syncDraftFromLayer();
  }

  private selectedColor(): string {
    const id = this.selectedId();
    const index = this.zones().findIndex(z => z.id === id);
    return this.color(index < 0 ? this.zones().length : index);
  }

  private clearDraftLayer(): void {
    this.draftLayer?.remove();
    this.draftLayer = undefined;
    this.haloLayer?.remove();
    this.haloLayer = undefined;
    this.map?.pm.disableDraw();
  }

  private center(): { lat: number; lng: number } {
    const lat = this.homeLat();
    const lng = this.homeLng();
    if (lat != null && lng != null) return { lat, lng };
    const drawn = this.zones().find(z => z.center);
    if (drawn?.center) return drawn.center;
    return { lat: 55.63, lng: 13.7 };
  }
}
