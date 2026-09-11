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

export function parseAnatomyMapValue(raw: unknown, fallbackPreset = DEFAULT_ANATOMY_PRESET): AnatomyMapValue {
  if (!raw) return { preset: fallbackPreset, annotations: [], strokes: [] };
  try {
    const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (!parsed || typeof parsed !== 'object') return { preset: fallbackPreset, annotations: [], strokes: [] };
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
