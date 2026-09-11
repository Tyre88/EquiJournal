import { Component, Input, Output, EventEmitter, computed, signal } from '@angular/core';

export interface BodyMapMarker {
  id: string;
  x: number;
  y: number;
  side: 'L' | 'R';
  label: string;
  note: string;
}

@Component({
  selector: 'app-bodymap',
  standalone: true,
  imports: [],
  template: `
    <div class="bodymap-container">
      <div class="bodymap-controls">
        <button
          type="button"
          class="side-toggle"
          [class.active]="currentSide() === 'L'"
          (click)="setSide('L')"
        >
          Vänster sida
        </button>
        <button
          type="button"
          class="side-toggle"
          [class.active]="currentSide() === 'R'"
          (click)="setSide('R')"
        >
          Höger sida
        </button>
      </div>

      <div class="bodymap-wrapper">
        <svg
          viewBox="0 0 800 500"
          class="bodymap-svg"
          [class.readonly]="readonly"
          (click)="onSvgClick($event)"
          (touchstart)="onTouchStart($event)"
        >
          <!-- Horse silhouette -->
          <g class="bodymap-horse">
            <!-- Head -->
            <path
              d="M620 80 Q650 60 680 70 Q710 80 720 100 L730 130 Q720 150 690 145 L660 140 Q640 130 630 110 Z"
              class="bodymap-area"
              data-side="L"
            />
            <!-- Neck -->
            <path
              d="M630 140 Q640 160 630 200 Q620 230 600 250 L580 240 Q590 200 600 160 Z"
              class="bodymap-area"
              data-side="L"
            />
            <!-- Shoulder -->
            <ellipse cx="540" cy="250" rx="70" ry="50" class="bodymap-area" data-side="L" />
            <!-- Chest -->
            <ellipse cx="480" cy="240" rx="50" ry="45" class="bodymap-area" data-side="L" />
            <!-- Front left leg -->
            <rect x="460" y="280" width="18" height="100" rx="8" class="bodymap-area" data-side="L" />
            <!-- Front right leg -->
            <rect x="490" y="280" width="18" height="100" rx="8" class="bodymap-area" data-side="L" />
            <!-- Body (barrel) -->
            <ellipse cx="400" cy="240" rx="140" ry="70" class="bodymap-area" data-side="L" />
            <!-- Withers -->
            <path d="M380 175 Q390 165 410 175" class="bodymap-area" data-side="L" />
            <!-- Back -->
            <ellipse cx="300" cy="230" rx="80" ry="60" class="bodymap-area" data-side="L" />
            <!-- Hindquarters -->
            <ellipse cx="180" cy="230" rx="90" ry="65" class="bodymap-area" data-side="L" />
            <!-- Hind left leg -->
            <rect x="130" y="280" width="18" height="100" rx="8" class="bodymap-area" data-side="L" />
            <!-- Hind right leg -->
            <rect x="160" y="280" width="18" height="100" rx="8" class="bodymap-area" data-side="L" />
            <!-- Tail -->
            <path
              d="M90 200 Q60 210 40 250 Q30 280 50 300"
              fill="none"
              stroke="currentColor"
              stroke-width="4"
              class="bodymap-area"
              data-side="L"
            />
            <!-- Center line -->
            <line x1="400" y1="175" x2="400" y2="380" stroke="#ccc" stroke-width="1" stroke-dasharray="5,5" />
            <!-- Side labels -->
            <text x="40" y="250" class="side-label">Vänster</text>
            <text x="680" y="250" class="side-label">Höger</text>
          </g>

          <!-- Markers -->
          @for (side of ['L', 'R']; track side) {
            @for (m of markers; track m.id) {
              @if (m.side === side) {
                <g class="marker" (click)="onMarkerClick(m, $event)" (touchstart)="onMarkerTouch(m, $event)">
                  <circle
                    [attr.cx]="getMarkerX(m, side)"
                    [attr.cy]="getMarkerY(m, side)"
                    r="14"
                    fill="#2d5016"
                    fill-opacity="0.2"
                    stroke="#2d5016"
                    stroke-width="2"
                  />
                  <circle
                    [attr.cx]="getMarkerX(m, side)"
                    [attr.cy]="getMarkerY(m, side)"
                    r="8"
                    fill="#2d5016"
                  />
                  <text
                    [attr.x]="getMarkerX(m, side)"
                    [attr.y]="getMarkerY(m, side) - 20"
                    text-anchor="middle"
                    fill="#2d5016"
                    font-size="12"
                    font-weight="bold"
                  >
                    {{ m.id }}
                  </text>
                </g>
              }
            }
          }
        </svg>
      </div>

      <!-- Marker list -->
      @if (markers.length > 0) {
        <div class="marker-list">
          <h3>Markörer</h3>
          @for (m of markers; track m.id) {
            <div class="marker-item">
              <span class="marker-num">{{ m.id }}</span>
              <span class="marker-side">{{ m.side === 'L' ? 'V' : 'H' }}</span>
              <span class="marker-label">{{ m.label || '—' }}</span>
              @if (m.note) {
                <span class="marker-note">{{ m.note }}</span>
              }
              @if (!readonly) {
                <button
                  type="button"
                  class="marker-remove"
                  (click)="removeMarker(m.id)"
                  aria-label="Ta bort markör {{ m.id }}"
                >
                  ×
                </button>
              }
            </div>
          }
        </div>
      }

      <!-- New marker form -->
      @if (editingMarkerId()) {
        <div class="marker-form">
          <h3>Markör {{ editingMarkerId() }}</h3>
          <div class="form-row">
            <label>Etikett</label>
            <input
              type="text"
              [value]="editingMarker()?.label ?? ''"
              (input)="onLabelInput($event)"
              placeholder="t.ex. Ömhet, Svullnad..."
            />
          </div>
          <div class="form-row">
            <label>Anteckning</label>
            <input
              type="text"
              [value]="editingMarker()?.note ?? ''"
              (input)="onNoteInput($event)"
              placeholder="Valfri notering..."
            />
          </div>
          <div class="form-actions">
            <button type="button" class="btn-save" (click)="saveMarker()">Spara</button>
            <button type="button" class="btn-cancel" (click)="cancelMarker()">Avbryt</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .bodymap-container {
      border: 1px solid #ddd;
      border-radius: 8px;
      overflow: hidden;
    }
    .bodymap-controls {
      display: flex;
      gap: 0.25rem;
      padding: 0.5rem;
      background: #f9f9f9;
      border-bottom: 1px solid #ddd;
    }
    .side-toggle {
      flex: 1;
      padding: 0.5rem 1rem;
      border: 1px solid #ddd;
      background: white;
      cursor: pointer;
      font-size: 0.875rem;
      border-radius: 4px;
      transition: all 0.2s;
    }
    .side-toggle.active {
      background: var(--color-primary);
      color: white;
      border-color: var(--color-primary);
    }
    .bodymap-wrapper {
      width: 100%;
      padding: 1rem;
      background: white;
    }
    .bodymap-svg {
      width: 100%;
      height: auto;
      display: block;
      cursor: crosshair;
      max-width: 800px;
      margin: 0 auto;
    }
    .bodymap-svg.readonly {
      cursor: default;
    }
    .bodymap-area {
      fill: #e8e8e8;
      stroke: #999;
      stroke-width: 1;
      transition: fill 0.2s;
    }
    .bodymap-area:hover {
      fill: #d4edcc;
    }
    .side-label {
      fill: #666;
      font-size: 14px;
      font-weight: 500;
    }
    .marker {
      cursor: pointer;
    }
    .marker-list {
      padding: 0.75rem;
      border-top: 1px solid #ddd;
      background: #fafafa;
    }
    .marker-list h3 {
      margin: 0 0 0.5rem;
      font-size: 0.875rem;
      color: #666;
    }
    .marker-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.5rem;
      background: white;
      border: 1px solid #eee;
      border-radius: 4px;
      margin-bottom: 0.25rem;
      font-size: 0.875rem;
    }
    .marker-num {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      background: var(--color-primary);
      color: white;
      border-radius: 50%;
      font-weight: bold;
      font-size: 0.75rem;
    }
    .marker-side {
      background: #eee;
      padding: 0.125rem 0.375rem;
      border-radius: 3px;
      font-size: 0.75rem;
      font-weight: 500;
    }
    .marker-label {
      font-weight: 500;
    }
    .marker-note {
      color: #666;
      font-style: italic;
      font-size: 0.75rem;
    }
    .marker-remove {
      margin-left: auto;
      background: none;
      border: none;
      font-size: 1.25rem;
      color: #999;
      cursor: pointer;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
    }
    .marker-remove:hover {
      color: var(--color-danger);
      background: #ffebee;
    }
    .marker-form {
      padding: 1rem;
      border-top: 1px solid #ddd;
      background: #f0f7e8;
    }
    .marker-form h3 {
      margin: 0 0 0.75rem;
      font-size: 0.875rem;
    }
    .form-row {
      margin-bottom: 0.5rem;
    }
    .form-row label {
      display: block;
      font-size: 0.75rem;
      font-weight: 500;
      color: #666;
      margin-bottom: 0.125rem;
    }
    .form-row input {
      width: 100%;
      padding: 0.5rem;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 0.875rem;
      box-sizing: border-box;
    }
    .form-actions {
      display: flex;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .btn-save {
      padding: 0.5rem 1rem;
      background: var(--color-primary);
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.875rem;
    }
    .btn-cancel {
      padding: 0.5rem 1rem;
      background: white;
      color: #333;
      border: 1px solid #ddd;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.875rem;
    }
    @media (max-width: 390px) {
      .side-toggle {
        padding: 0.75rem;
        font-size: 1rem;
      }
      .marker-item {
        padding: 0.75rem;
        font-size: 1rem;
      }
      .marker-remove {
        padding: 0.5rem;
      }
    }
  `]
})
export class BodymapComponent {
  @Input() markers: BodyMapMarker[] = [];
  @Input() readonly = false;
  @Output() add = new EventEmitter<{ x: number; y: number; side: 'L' | 'R' }>();
  @Output() labelChange = new EventEmitter<{ id: string; label: string }>();
  @Output() noteChange = new EventEmitter<{ id: string; note: string }>();
  @Output() remove = new EventEmitter<string>();

  currentSide = signal<'L' | 'R'>('L');
  editingMarkerId = signal<string | null>(null);
  editingMarker = computed(() => {
    const id = this.editingMarkerId();
    if (!id) return null;
    return this.markers.find(m => m.id === id) ?? null;
  });

  private nextId = 1;

  setSide(side: 'L' | 'R'): void {
    this.currentSide.set(side);
  }

  onSvgClick(event: MouseEvent): void {
    this.handlePoint(event);
  }

  onTouchStart(event: TouchEvent): void {
    if (event.touches.length === 1) {
      const touch = event.touches[0];
      const fakeEvent = {
        clientX: touch.clientX,
        clientY: touch.clientY,
        target: touch.target
      } as MouseEvent;
      this.handlePoint(fakeEvent);
    }
  }

  private handlePoint(event: MouseEvent): void {
    if (this.readonly) return;

    const svg = event.target instanceof SVGElement
      ? event.target.closest('svg')
      : null;

    if (!svg) return;

    const rect = svg.getBoundingClientRect();
    const viewBox = svg.viewBox.baseVal;
    const x = ((event.clientX - rect.left) / rect.width) * viewBox.width;
    const y = ((event.clientY - rect.top) / rect.height) * viewBox.height;

    const side = x < viewBox.width / 2 ? 'L' : 'R';

    this.add.emit({ x, y, side });

    const newId = String(this.nextId++);
    this.editingMarkerId.set(newId);
  }

  onMarkerClick(marker: BodyMapMarker, event: MouseEvent): void {
    event.stopPropagation();
    this.editingMarkerId.set(marker.id);
  }

  onMarkerTouch(marker: BodyMapMarker, event: TouchEvent): void {
    event.stopPropagation();
    event.preventDefault();
    this.editingMarkerId.set(marker.id);
  }

  removeMarker(id: string): void {
    this.remove.emit(id);
  }

  onLabelInput(event: Event): void {
    const id = this.editingMarkerId();
    const value = (event.target as HTMLInputElement).value;
    if (id) {
      this.labelChange.emit({ id, label: value });
    }
  }

  onNoteInput(event: Event): void {
    const id = this.editingMarkerId();
    const value = (event.target as HTMLInputElement).value;
    if (id) {
      this.noteChange.emit({ id, note: value });
    }
  }

  saveMarker(): void {
    this.editingMarkerId.set(null);
  }

  cancelMarker(): void {
    this.editingMarkerId.set(null);
  }

  getMarkerX(marker: BodyMapMarker, side: 'L' | 'R'): number {
    if (marker.side === side) {
      return marker.x > 400 ? marker.x - 300 : marker.x;
    }
    return marker.side === 'R' ? marker.x - 400 : marker.x + 400;
  }

  getMarkerY(marker: BodyMapMarker, _side: 'L' | 'R'): number {
    return marker.y;
  }
}

