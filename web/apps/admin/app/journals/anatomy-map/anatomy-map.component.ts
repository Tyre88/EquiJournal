import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AnatomyAnnotation,
  AnatomyRegion,
  AnatomyStroke,
  DEFAULT_FINDING_OPTIONS
} from './anatomy-map.types';
import { getAnatomyPreset } from './presets/horse-muscles-standard';

type MapMode = 'mark' | 'draw';

interface DrawPoint {
  x: number;
  y: number;
}

const DRAW_COLORS = [
  { id: 'red', value: '#c0392b', label: 'Röd' },
  { id: 'blue', value: '#2471a3', label: 'Blå' },
  { id: 'black', value: '#1f1812', label: 'Svart' }
] as const;

const STROKE_WIDTH = 1.2;

@Component({
  selector: 'app-anatomy-map',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="anatomy-map">
      <p class="hint">{{ hintText }}</p>
      @if (!readonly) {
        <div class="toolbar" role="toolbar" aria-label="Anatomikarta">
          <div class="mode-toggle" role="group" aria-label="Läge">
            <button
              type="button"
              class="mode-btn"
              [class.active]="mode() === 'mark'"
              (click)="setMode('mark')"
            >Markera</button>
            <button
              type="button"
              class="mode-btn"
              [class.active]="mode() === 'draw'"
              (click)="setMode('draw')"
            >Rita</button>
          </div>
          @if (mode() === 'draw') {
            <div class="colors" role="group" aria-label="Färg">
              @for (c of drawColors; track c.id) {
                <button
                  type="button"
                  class="color-chip"
                  [class.active]="drawColor() === c.value"
                  [style.background]="c.value"
                  [attr.aria-label]="c.label"
                  (click)="drawColor.set(c.value)"
                ></button>
              }
            </div>
            <button
              type="button"
              class="tool-btn"
              (click)="undoStroke()"
              [disabled]="strokes.length === 0"
            >Ångra</button>
            <button
              type="button"
              class="tool-btn"
              (click)="clearStrokes()"
              [disabled]="strokes.length === 0"
            >Rensa ritning</button>
          }
        </div>
      }
      <div class="diagram-wrap">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          [attr.viewBox]="viewBoxAttr"
          [style.aspect-ratio]="aspectRatio"
          preserveAspectRatio="xMidYMid meet"
          class="diagram"
          [class.readonly]="readonly"
          [class.drawing]="isDrawingEnabled"
          role="img"
          aria-label="Anatomikarta, hästmuskler"
          (pointerdown)="onPointerDown($event)"
          (pointermove)="onPointerMove($event)"
          (pointerup)="onPointerUp($event)"
          (pointercancel)="onPointerUp($event)"
        >
          <svg:rect
            x="0"
            y="0"
            [attr.width]="diagram.viewBox.width"
            [attr.height]="diagram.viewBox.height"
            fill="#efe7d6"
          ></svg:rect>

          @if (customImageUrl) {
            <svg:image
              [attr.href]="customImageUrl"
              x="0"
              y="0"
              [attr.width]="diagram.viewBox.width"
              [attr.height]="diagram.viewBox.height"
              preserveAspectRatio="xMidYMid meet"
            ></svg:image>
          } @else {
            @for (s of diagram.silhouettes; track $index) {
              <svg:path
                [attr.d]="s.d"
                [attr.fill]="s.fill ?? '#8a7354'"
                [attr.stroke]="s.stroke ?? '#1f1812'"
                [attr.stroke-width]="s.strokeWidth ?? 0.7"
                stroke-linecap="round"
                stroke-linejoin="round"
              ></svg:path>
            }
            @for (lbl of diagram.sideLabels; track lbl.text) {
              <svg:text
                [attr.x]="lbl.x"
                [attr.y]="lbl.y"
                [attr.text-anchor]="lbl.anchor ?? 'start'"
                fill="#1f1812"
                font-size="4"
                font-weight="700"
              >{{ lbl.text }}</svg:text>
            }
          }

          @for (region of diagram.regions; track region.id) {
            <svg:polygon
              [attr.points]="region.points"
              [attr.data-region-id]="region.id"
              [attr.fill]="regionFill(region.id)"
              [attr.stroke]="regionStroke(region.id)"
              stroke-width="0.28"
              [style.pointer-events]="isDrawingEnabled ? 'none' : 'auto'"
              style="cursor:pointer"
              (click)="onRegionClick(region, $event)"
            >
              <svg:title>{{ region.label }} ({{ region.side === 'L' ? 'V' : 'H' }})</svg:title>
            </svg:polygon>
          }

          @for (stroke of strokes; track stroke.id) {
            <svg:path
              [attr.d]="stroke.d"
              fill="none"
              [attr.stroke]="stroke.color"
              [attr.stroke-width]="stroke.width"
              stroke-linecap="round"
              stroke-linejoin="round"
              pointer-events="none"
            ></svg:path>
          }
          @if (draftPath()) {
            <svg:path
              [attr.d]="draftPath()"
              fill="none"
              [attr.stroke]="drawColor()"
              [attr.stroke-width]="strokeWidth"
              stroke-linecap="round"
              stroke-linejoin="round"
              pointer-events="none"
            ></svg:path>
          }
        </svg>
      </div>

      @if (selectedRegion(); as region) {
        <div class="sheet" role="dialog" [attr.aria-label]="'Anteckning för ' + region.label">
          <header>
            <h3>{{ region.label }}</h3>
            <span class="side-chip">{{ region.side === 'L' ? 'Vänster' : 'Höger' }}</span>
            <button type="button" class="icon-btn" (click)="closeSheet()" aria-label="Stäng">×</button>
          </header>

          @if (!readonly) {
            <div class="findings" role="group" aria-label="Fynd">
              @for (opt of findings; track opt) {
                <button
                  type="button"
                  class="finding"
                  [class.active]="draftFinding() === opt"
                  (click)="draftFinding.set(opt)"
                >{{ opt }}</button>
              }
            </div>
            <label class="note-label" [attr.for]="'note-' + region.id">Anteckning</label>
            <input
              [id]="'note-' + region.id"
              type="text"
              class="note"
              [value]="draftNote()"
              (input)="draftNote.set($any($event.target).value)"
              placeholder="Valfri notering…"
            />
            <div class="sheet-actions">
              <button type="button" class="btn-clear" (click)="clearSelected()" [disabled]="!isAnnotated(region.id)">
                Rensa
              </button>
              <button type="button" class="btn-save" (click)="saveSelected()" [disabled]="!draftFinding()">
                Spara
              </button>
            </div>
          } @else {
            <p class="readonly-finding">
              {{ annotationFor(region.id)?.finding || '—' }}
              @if (annotationFor(region.id)?.note) {
                <span class="readonly-note"> — {{ annotationFor(region.id)!.note }}</span>
              }
            </p>
          }
        </div>
      }

      @if (annotations.length > 0) {
        <ul class="ann-list">
          @for (a of annotations; track a.regionId) {
            <li>
              <button type="button" class="ann-item" (click)="selectById(a.regionId)">
                <span class="ann-name">{{ a.label }}</span>
                <span class="side-chip">{{ a.side === 'L' ? 'V' : 'H' }}</span>
                <span class="ann-finding">{{ a.finding }}</span>
                @if (a.note) {
                  <span class="ann-note">{{ a.note }}</span>
                }
              </button>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .anatomy-map {
      border: 1px solid var(--color-border, #ddd);
      border-radius: 8px;
      overflow: hidden;
      background: var(--color-surface, #fff);
      color: var(--color-text, #243018);
    }
    .hint {
      margin: 0;
      padding: 0.65rem 0.85rem 0;
      font-size: 0.85rem;
      color: var(--color-text-muted, #5c6454);
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 0.85rem 0;
    }
    .mode-toggle { display: flex; gap: 0.25rem; }
    .mode-btn, .tool-btn {
      min-height: 40px;
      padding: 0.35rem 0.75rem;
      border: 1px solid var(--color-border-strong, #ccc);
      background: var(--color-surface, #fff);
      color: inherit;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.85rem;
    }
    .mode-btn.active {
      background: var(--color-primary, #2d5016);
      color: var(--color-text-inverse, #fff);
      border-color: var(--color-primary, #2d5016);
    }
    .tool-btn:disabled { opacity: 0.45; cursor: default; }
    .colors { display: flex; gap: 0.35rem; }
    .color-chip {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      border: 2px solid transparent;
      cursor: pointer;
      padding: 0;
    }
    .color-chip.active { border-color: var(--color-text, #243018); box-shadow: 0 0 0 2px #fff inset; }
    .diagram-wrap {
      padding: 0.5rem 0.75rem 0.85rem;
      background: #efe7d6;
    }
    .diagram {
      display: block;
      width: 100%;
      max-width: 720px;
      margin: 0 auto;
      min-height: 200px;
      height: auto;
    }
    .diagram.drawing {
      touch-action: none;
      cursor: crosshair;
    }
    .sheet {
      padding: 0.85rem 1rem 1rem;
      border-top: 1px solid var(--color-border, #ddd);
      background: var(--color-primary-soft, #f0f7e8);
    }
    .sheet header { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.65rem; }
    .sheet h3 { margin: 0; font-size: 1rem; flex: 1; }
    .side-chip {
      background: var(--color-surface-muted, #e8e8e8);
      padding: 0.15rem 0.45rem;
      border-radius: 3px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .icon-btn {
      border: 0; background: none; font-size: 1.4rem; line-height: 1;
      cursor: pointer; min-width: 44px; min-height: 44px; color: inherit;
    }
    .findings { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-bottom: 0.65rem; }
    .finding {
      min-height: 44px; padding: 0.4rem 0.85rem;
      border: 1px solid var(--color-border-strong, #ccc);
      background: var(--color-surface, #fff); color: inherit;
      border-radius: 999px; cursor: pointer; font-size: 0.9rem;
    }
    .finding.active {
      background: var(--color-primary, #2d5016);
      color: var(--color-text-inverse, #fff);
      border-color: var(--color-primary, #2d5016);
    }
    .note-label { display: block; font-size: 0.75rem; font-weight: 600; color: var(--color-text-muted, #555); margin-bottom: 0.2rem; }
    .note {
      width: 100%; box-sizing: border-box; min-height: 44px; padding: 0.5rem 0.65rem;
      border: 1px solid var(--color-border-strong, #ccc); border-radius: 4px; font-size: 1rem;
      background: var(--color-surface, #fff); color: inherit;
    }
    .sheet-actions { display: flex; gap: 0.5rem; margin-top: 0.75rem; }
    .btn-save, .btn-clear { min-height: 44px; padding: 0.5rem 1rem; border-radius: 4px; cursor: pointer; font-size: 0.95rem; }
    .btn-save { background: var(--color-primary, #2d5016); color: #fff; border: 0; margin-left: auto; }
    .btn-save:disabled { opacity: 0.5; cursor: default; }
    .btn-clear { background: var(--color-surface, #fff); border: 1px solid var(--color-border-strong, #ccc); color: inherit; }
    .btn-clear:disabled { opacity: 0.45; cursor: default; }
    .readonly-finding { margin: 0; font-size: 0.95rem; }
    .readonly-note { color: var(--color-text-muted, #555); }
    .ann-list { list-style: none; margin: 0; padding: 0.5rem 0.75rem 0.75rem; border-top: 1px solid var(--color-border, #ddd); }
    .ann-item {
      width: 100%; display: flex; flex-wrap: wrap; align-items: center; gap: 0.4rem;
      text-align: left; background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #eee); border-radius: 4px;
      padding: 0.55rem 0.65rem; margin-bottom: 0.35rem; cursor: pointer;
      min-height: 44px; font-size: 0.9rem; color: inherit;
    }
    .ann-name { font-weight: 600; }
    .ann-finding { color: var(--color-primary, #2d5016); font-weight: 600; }
    .ann-note { color: var(--color-text-muted, #666); font-style: italic; }
  `]
})
export class AnatomyMapComponent {
  @Input() presetId = 'horse-muscles-standard';
  @Input() customImageUrl: string | null = null;
  @Input() findingOptions: string[] = DEFAULT_FINDING_OPTIONS;
  @Input() annotations: AnatomyAnnotation[] = [];
  @Input() strokes: AnatomyStroke[] = [];
  @Input() readonly = false;
  @Output() annotationChange = new EventEmitter<AnatomyAnnotation[]>();
  @Output() strokesChange = new EventEmitter<AnatomyStroke[]>();

  readonly drawColors = DRAW_COLORS;
  readonly strokeWidth = STROKE_WIDTH;

  mode = signal<MapMode>('mark');
  drawColor = signal<string>(DRAW_COLORS[0].value);
  draftPath = signal('');
  selectedId = signal<string | null>(null);
  draftFinding = signal('');
  draftNote = signal('');

  private drawing = false;
  private draftPoints: DrawPoint[] = [];

  get diagram() {
    return getAnatomyPreset(this.presetId);
  }

  get viewBoxAttr(): string {
    const { width, height } = this.diagram.viewBox;
    return `0 0 ${width} ${height}`;
  }

  get aspectRatio(): string {
    const { width, height } = this.diagram.viewBox;
    return `${width} / ${height}`;
  }

  get findings(): string[] {
    return this.findingOptions?.length ? this.findingOptions : DEFAULT_FINDING_OPTIONS;
  }

  get isDrawingEnabled(): boolean {
    return !this.readonly && this.mode() === 'draw';
  }

  get hintText(): string {
    if (this.readonly) return 'Markerade områden och ritning.';
    return this.mode() === 'draw'
      ? 'Rita på bilden med fingret eller musen.'
      : 'Tryck på ett muskelområde för att anteckna ett fynd.';
  }

  selectedRegion = computed(() => {
    const id = this.selectedId();
    if (!id) return null;
    return this.diagram.regions.find(r => r.id === id) ?? null;
  });

  setMode(mode: MapMode): void {
    this.mode.set(mode);
    if (mode === 'draw') this.closeSheet();
  }

  regionFill(id: string): string {
    if (this.isAnnotated(id)) return 'rgba(45,80,22,0.38)';
    if (this.selectedId() === id) return 'rgba(45,80,22,0.22)';
    return 'rgba(45,80,22,0.10)';
  }

  regionStroke(id: string): string {
    return this.isAnnotated(id) || this.selectedId() === id ? '#2d5016' : 'rgba(31,24,18,0.45)';
  }

  isAnnotated(id: string): boolean {
    return this.annotations.some(a => a.regionId === id);
  }

  annotationFor(id: string): AnatomyAnnotation | undefined {
    return this.annotations.find(a => a.regionId === id);
  }

  onRegionClick(region: AnatomyRegion, event: Event): void {
    if (this.isDrawingEnabled) return;
    event.preventDefault();
    event.stopPropagation();
    this.openRegion(region);
  }

  selectById(id: string): void {
    const region = this.diagram.regions.find(r => r.id === id);
    if (region) this.openRegion(region);
  }

  closeSheet(): void {
    this.selectedId.set(null);
  }

  saveSelected(): void {
    const region = this.selectedRegion();
    const finding = this.draftFinding().trim();
    if (!region || !finding || this.readonly) return;
    const next = this.annotations.filter(a => a.regionId !== region.id);
    next.push({
      regionId: region.id,
      label: region.label,
      side: region.side,
      finding,
      note: this.draftNote().trim()
    });
    this.annotationChange.emit(next);
    this.selectedId.set(null);
  }

  clearSelected(): void {
    const region = this.selectedRegion();
    if (!region || this.readonly) return;
    this.annotationChange.emit(this.annotations.filter(a => a.regionId !== region.id));
    this.selectedId.set(null);
  }

  onPointerDown(event: PointerEvent): void {
    if (!this.isDrawingEnabled) return;
    if (event.pointerType === 'mouse' && event.button !== 0) return;
    event.preventDefault();
    const pt = this.toSvgPoint(event);
    if (!pt) return;
    try {
      (event.currentTarget as Element).setPointerCapture(event.pointerId);
    } catch {
      // Capture can fail for untrusted or already-released pointers.
    }
    this.drawing = true;
    this.draftPoints = [pt];
    this.draftPath.set(pointsToPath(this.draftPoints));
  }

  onPointerMove(event: PointerEvent): void {
    if (!this.drawing) return;
    event.preventDefault();
    const pt = this.toSvgPoint(event);
    if (!pt) return;
    const last = this.draftPoints[this.draftPoints.length - 1];
    if (last && distance(last, pt) < 0.25) return;
    this.draftPoints.push(pt);
    this.draftPath.set(pointsToPath(this.draftPoints));
  }

  onPointerUp(event: PointerEvent): void {
    if (!this.drawing) return;
    event.preventDefault();
    this.drawing = false;
    const points = this.draftPoints;
    this.draftPoints = [];
    this.draftPath.set('');
    if (points.length === 0) return;
    const stroke: AnatomyStroke = {
      id: nextStrokeId(),
      color: this.drawColor(),
      width: STROKE_WIDTH,
      d: pointsToPath(points.length === 1 ? [points[0], { x: points[0].x + 0.01, y: points[0].y }] : points)
    };
    this.strokesChange.emit([...this.strokes, stroke]);
  }

  undoStroke(): void {
    if (this.readonly || this.strokes.length === 0) return;
    this.strokesChange.emit(this.strokes.slice(0, -1));
  }

  clearStrokes(): void {
    if (this.readonly || this.strokes.length === 0) return;
    this.strokesChange.emit([]);
  }

  private openRegion(region: AnatomyRegion): void {
    this.selectedId.set(region.id);
    const existing = this.annotationFor(region.id);
    this.draftFinding.set(existing?.finding ?? '');
    this.draftNote.set(existing?.note ?? '');
  }

  private toSvgPoint(event: PointerEvent): DrawPoint | null {
    const svg = event.currentTarget as SVGSVGElement | null;
    if (!svg?.createSVGPoint) return null;
    const ctm = svg.getScreenCTM();
    if (!ctm) return null;
    const pt = svg.createSVGPoint();
    pt.x = event.clientX;
    pt.y = event.clientY;
    const mapped = pt.matrixTransform(ctm.inverse());
    return { x: mapped.x, y: mapped.y };
  }
}

function pointsToPath(points: DrawPoint[]): string {
  if (points.length === 0) return '';
  const first = points[0];
  let d = `M ${round(first.x)} ${round(first.y)}`;
  for (let i = 1; i < points.length; i++) {
    d += ` L ${round(points[i].x)} ${round(points[i].y)}`;
  }
  return d;
}

function round(n: number): number {
  return Math.round(n * 100) / 100;
}

function distance(a: DrawPoint, b: DrawPoint): number {
  const dx = a.x - b.x;
  const dy = a.y - b.y;
  return Math.sqrt(dx * dx + dy * dy);
}

function nextStrokeId(): string {
  return `stroke_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}
