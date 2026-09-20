export type AnatomySide = 'L' | 'R';

export interface AnatomyRegion {
  id: string;
  label: string;
  side: AnatomySide;
  points: string;
}

export interface AnatomyAnnotation {
  regionId: string;
  label: string;
  side: AnatomySide;
  finding: string;
  note: string;
}

export interface AnatomyStroke {
  id: string;
  color: string;
  width: number;
  d: string;
}

export interface AnatomyMapValue {
  preset: string;
  customImageKey?: string | null;
  annotations: AnatomyAnnotation[];
  strokes: AnatomyStroke[];
}

export interface AnatomySilhouette {
  d: string;
  /** Fill colour; defaults to the standard body tone when omitted. */
  fill?: string;
  /** Stroke colour; defaults to the standard outline colour when omitted. */
  stroke?: string;
  strokeWidth?: number;
}

export interface AnatomySideLabel {
  text: string;
  x: number;
  y: number;
  anchor?: 'start' | 'middle' | 'end';
}

export interface AnatomyPreset {
  id: string;
  label: string;
  viewBox: { width: number; height: number };
  silhouettes: AnatomySilhouette[];
  sideLabels: AnatomySideLabel[];
  regions: AnatomyRegion[];
}

export const DEFAULT_ANATOMY_PRESET = 'horse-muscles-standard';
export const DEFAULT_FINDING_OPTIONS = [
  'Ua', 'Öm', 'Spänd', 'Svullen',
  'Galla', 'Triggerpunkt', 'Sår', 'Knöl', 'Muskelknuta'
];

/** Legacy bodymap marker shape stored in older journal template data. */
export interface LegacyBodyMapMarker {
  id: string;
  x: number;
  y: number;
  side: AnatomySide;
  label: string;
  note: string;
}

export function isBodyMapValue(value: unknown): boolean {
  const parsed = unwrapJsonValue(value);
  if (!parsed || typeof parsed !== 'object') return false;
  const markers = Array.isArray(parsed)
    ? parsed
    : (parsed as { markers?: unknown }).markers;
  if (!Array.isArray(markers) || markers.length === 0) return false;
  return markers.every(m =>
    !!m && typeof m === 'object' && 'x' in m && 'y' in m && 'side' in m
  );
}

export function parseBodyMapMarkers(raw: unknown): LegacyBodyMapMarker[] {
  const parsed = unwrapJsonValue(raw);
  if (!parsed || typeof parsed !== 'object') return [];
  const markers = Array.isArray(parsed)
    ? parsed
    : (parsed as { markers?: unknown }).markers;
  if (!Array.isArray(markers)) return [];
  return markers.filter((m): m is LegacyBodyMapMarker =>
    !!m && typeof m === 'object'
    && typeof (m as LegacyBodyMapMarker).x === 'number'
    && typeof (m as LegacyBodyMapMarker).y === 'number'
    && ((m as LegacyBodyMapMarker).side === 'L' || (m as LegacyBodyMapMarker).side === 'R')
  );
}

/** Read-path shim: legacy bodymap marker JSON → anatomy-map value (list-only; no region highlights). */
export function coerceBodyMapToAnatomyValue(
  raw: unknown,
  fallbackPreset = DEFAULT_ANATOMY_PRESET
): AnatomyMapValue {
  const markers = parseBodyMapMarkers(raw);
  return {
    preset: fallbackPreset,
    annotations: markers.map((marker, index) => ({
      regionId: `legacy-bodymap-${marker.id || index}`,
      label: marker.label?.trim() || `Markering ${index + 1}`,
      side: marker.side,
      finding: marker.label?.trim() || 'Legacy',
      note: marker.note?.trim() ?? ''
    })),
    strokes: []
  };
}

export function normalizeTemplateSectionType(type: string | undefined | null): string {
  const normalized = String(type ?? '').trim().toLowerCase();
  if (normalized === 'bodymap' || normalized === 'anatomymap') return 'anatomy-map';
  return normalized;
}

export function isTemplateMapSection(section: {
  type?: string;
  preset?: string;
  findingOptions?: unknown;
}): boolean {
  const type = normalizeTemplateSectionType(section.type);
  if (type === 'anatomy-map') return true;
  return Array.isArray(section.findingOptions) || !!section.preset;
}

export function parseAnatomyMapValue(raw: unknown, fallbackPreset = DEFAULT_ANATOMY_PRESET): AnatomyMapValue {
  if (!raw) return { preset: fallbackPreset, annotations: [], strokes: [] };
  if (isBodyMapValue(raw)) return coerceBodyMapToAnatomyValue(raw, fallbackPreset);
  try {
    const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (!parsed || typeof parsed !== 'object') return { preset: fallbackPreset, annotations: [], strokes: [] };
    if (isBodyMapValue(parsed)) return coerceBodyMapToAnatomyValue(parsed, fallbackPreset);
    const obj = parsed as Partial<AnatomyMapValue>;
    const annotations = Array.isArray(obj.annotations)
      ? obj.annotations.filter((a): a is AnatomyAnnotation =>
          !!a && typeof a === 'object' && typeof a.regionId === 'string')
      : [];
    const strokes = Array.isArray(obj.strokes)
      ? obj.strokes.filter(isAnatomyStroke).map(s => ({
          id: s.id,
          color: s.color,
          width: s.width,
          d: s.d
        }))
      : [];
    return {
      preset: typeof obj.preset === 'string' && obj.preset ? obj.preset : fallbackPreset,
      customImageKey: typeof obj.customImageKey === 'string' ? obj.customImageKey : null,
      annotations,
      strokes
    };
  } catch {
    return { preset: fallbackPreset, annotations: [], strokes: [] };
  }
}

function isAnatomyStroke(value: unknown): value is AnatomyStroke {
  if (!value || typeof value !== 'object') return false;
  const s = value as Partial<AnatomyStroke>;
  return typeof s.d === 'string' && s.d.length > 0
    && typeof s.color === 'string' && s.color.length > 0
    && typeof s.width === 'number' && s.width > 0
    && typeof s.id === 'string' && s.id.length > 0;
}

export function isAnatomyMapValue(value: unknown): value is AnatomyMapValue {
  if (isBodyMapValue(value)) return true;
  if (!value || typeof value !== 'object') {
    if (typeof value !== 'string') return false;
    try {
      return isAnatomyMapValue(JSON.parse(value));
    } catch {
      return false;
    }
  }
  return Array.isArray((value as AnatomyMapValue).annotations);
}

function unwrapJsonValue(value: unknown): unknown {
  if (typeof value !== 'string') return value;
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}
