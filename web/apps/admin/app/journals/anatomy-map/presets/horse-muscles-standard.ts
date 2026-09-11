import { AnatomyPreset, AnatomyRegion, AnatomySide, AnatomySilhouette } from '../anatomy-map.types';

/**
 * Two side-view horses with superficial musculature, drawn side by side like a
 * printed palpation sheet: the left horse faces left (showing its left side),
 * the right horse is a mirror image facing right (showing its right side).
 *
 * The horse is authored once, facing left, inside a 100×100 box. The right
 * horse is produced by mirroring every x-coordinate across the full viewBox.
 * Palpation regions are authored as paths that follow the drawn muscle
 * contours and are flattened to polygons at build time.
 */
const VIEW_WIDTH = 210;
const VIEW_HEIGHT = 100;

const INK = '#1f1812';
const BODY = '#d9c7a3';
const SHADE = '#c4ae86';

/** Body outline including the near-side front and hind legs. */
const BODY_OUTLINE =
  'M22 9 ' +
  'C29 6.5 40 11.5 47 21 ' +
  'C53 24.5 63 25 72 23 ' +
  'C77 22.4 83 22 88 24 ' +
  'C90.5 25 92 26.5 92.5 27.5 ' +
  'C95.5 30 97 36 97 44 ' +
  'C97 48 95.5 52 92 59 ' +
  'C90 62.5 88.5 65 88 67.5 ' +
  'C87 70 86.6 74 86.6 78 ' +
  'C86.6 82 86.5 84 86.5 85.5 ' +
  'C87.5 88 88.2 91 88 94 ' +
  'L81.5 94 ' +
  'C81.5 91.5 82 89 82.8 86 ' +
  'C83 82 82.8 76 82.6 72 ' +
  'C82.4 69 81 66 79 62.5 ' +
  'C78 60 76.5 57.5 75.5 55.5 ' +
  'C72 58 65 61 58 61.2 ' +
  'C51 61.4 45 59.5 39 55.5 ' +
  'C38.8 59 38.5 64 38.2 68 ' +
  'C38 70 37.6 72 37.4 73 ' +
  'C37.2 77 37 82 36.8 85.5 ' +
  'C37.5 88 38 91 38 94 ' +
  'L31.5 94 ' +
  'C31.5 91 32.4 88 33.3 85.5 ' +
  'C33.4 82 33.4 77 33.4 73.5 ' +
  'C33 70 32.4 66 31.6 61.5 ' +
  'C31 58 30 55.5 29.5 53 ' +
  'C28.5 49 27.5 45 27.6 41.5 ' +
  'C27.8 39 28.2 37.5 28.5 36.5 ' +
  'C27.6 33 25 29 22.6 25.3 ' +
  'C22 25.6 20.5 26.6 18.5 27.6 ' +
  'C15.5 29.2 11 29.4 7 29.8 ' +
  'C5.5 30 4 29.8 3 28.6 ' +
  'C2.2 27.4 2.2 25.6 3 24.5 ' +
  'C7 20.5 13 14.5 18 10.6 ' +
  'L18.5 10 L20.2 2.6 L22 9 Z';

/** Parts drawn behind the body (far-side ear/legs, tail). */
const BACKGROUND_PARTS: string[] = [
  // far ear
  'M21.5 10 L23.2 3.6 L25 10 Z',
  // far front leg
  'M34.5 56 L42.5 56 C42 64 41 70 40.8 74 C40.6 78 40.5 84 40.3 86 C41.2 90 41.8 92 41.8 94 ' +
    'L35.3 94 C35.3 92 35.8 90 37 86 C37.1 84 36.9 78 36.7 74 C36.2 68 35 62 34.5 56 Z',
  // far hind leg
  'M73 56 C75.5 60 78 64 79 68 C79.4 72 79.6 80 80 85 C79 89 78.3 92 78.3 94 ' +
    'L84.5 94 C84.5 92 84 89 83.5 85 C83.7 80 83.7 72 84.5 67 C86 62 88 58 90 54 Z',
  // tail
  'M91 27 C97 31 100 42 99 54 C98.6 60 99 66 99.6 70 C97 67 95 61 95.4 54 C95.8 46 95.4 36 92.4 29.4 Z'
];

/** Superficial muscle contours and other detail strokes (no fill). */
const MUSCLE_LINES: string[] = [
  // head: nostril, mouth, masseter, facial crest, temporal
  'M4.4 25 C5.4 24.2 6 25 5.6 26',
  'M3.6 28.4 C5 28.2 6.2 28.2 7.2 28.2',
  'M16.4 13.8 C20.8 19 20 26.6 15 29',
  'M10.8 19.4 C12.6 21.2 13.2 24.2 12.2 27.4',
  'M18 11.4 C18.8 13.4 19.6 15.4 19.4 17.2',
  // neck: splenius, brachiocephalicus, jugular groove, rhomboid/trapezius
  'M24.2 11.6 C32 13 41 17.4 46.6 24.6',
  'M21 13.6 C27 19.6 30 29.6 29.6 40.2',
  'M23.4 26 C25.8 30 27.6 34.6 28.4 39.4',
  'M27.4 15.2 C32.6 18.4 38 23.6 42 29.2',
  'M38 13 C40.6 18 43.2 22.6 44.6 26.8',
  // shoulder: spine of scapula, triceps, deltoid/pectoral
  'M47.2 24 C42 29.5 36 36.5 31 43.4',
  'M49.6 28.4 C49.6 38 45.6 47.6 39.4 55.4',
  'M29.6 45 C33 48 36.6 51.6 39.2 55.4',
  'M35.4 45.4 C37.6 48.4 38.8 51.6 39.2 55.4',
  // barrel: latissimus dorsi, serratus ventralis scallops, external oblique
  'M72.5 25.4 C63 32 53.5 38 45 42.6',
  'M45.6 44.6 Q49.4 48.8 53.2 45.6 Q57 49.6 60.8 46.2 Q64.6 50 68.4 46.8 Q72.2 50.4 75.4 48.4',
  'M50.4 49 C51.6 52 52.6 55 53.2 58.6',
  'M56.4 49.4 C57.6 52.4 58.6 55.6 59.2 59',
  'M62.4 49.6 C63.6 52.6 64.6 55.6 65.2 59',
  'M68.4 50 C69.4 52.6 70.2 55 70.8 57.6',
  'M44.6 52.4 C52 56.6 62 57.8 72.6 54.4',
  // hindquarters: tensor fasciae latae, gluteals, biceps femoris, semitendinosus, patella, gaskin
  'M79.2 23.4 C76.6 32 74.8 43 75.6 54',
  'M80.4 26.2 C83.6 33 85 40 82.6 52.4',
  'M81 24.2 C85 26 89 28.4 93 31',
  'M86.6 24.8 C89.6 36 89.6 48 86.2 60.4',
  'M91.2 27.6 C94.2 36 93.8 48 90.4 58.6',
  'M76.2 53 C77.8 52.4 78.8 53.4 78.8 55',
  'M80 60.6 C83 62.6 85.6 65 87.4 67.4',
  // legs: forearm, knee, tendons, hock, coronets
  'M34.6 60 C35.2 64 35.6 68 35.6 72',
  'M33.4 73.4 L37.4 73.4',
  'M36.2 75.5 C36.4 79 36.4 83 36.2 85.5',
  'M82.8 68.2 L88 68.2',
  'M85.6 70.5 C85.7 75 85.8 80 85.6 85',
  'M33.3 86.4 C35 86 36 86 36.8 86.4',
  'M82.8 86.6 C84.5 86.2 85.5 86.2 86.5 86.6'
];

/** Eye, drawn as a filled shape. */
const EYE = 'M12.4 15.4 C12.4 14.2 14.8 14.2 14.8 15.4 C14.8 16.6 12.4 16.6 12.4 15.4 Z';

interface RegionDef {
  key: string;
  label: string;
  /** Closed path (absolute M/L/C/Q/Z) that follows the drawn contours. */
  d: string;
}

/**
 * Palpation regions for the left-facing horse. Curve segments reuse the
 * control points of the outline and muscle lines above (split with de
 * Casteljau where a region boundary ends part-way along a line).
 */
const LEFT_REGIONS: RegionDef[] = [
  {
    key: 'huvud',
    label: 'Huvud',
    d:
      'M22 9 L21 13.6 L22.6 25.3 C22 25.6 20.5 26.6 18.5 27.6 C15.5 29.2 11 29.4 7 29.8 ' +
      'C5.5 30 4 29.8 3 28.6 C2.2 27.4 2.2 25.6 3 24.5 C7 20.5 13 14.5 18 10.6 L18.5 10 Z'
  },
  {
    key: 'nacke',
    label: 'Nacke (splenius/trapezius)',
    d:
      'M22 9 C29 6.5 40 11.5 47 21 L47.2 24 C42 29.5 36 36.5 31 43.4 L29.6 40.2 ' +
      'C30 29.6 27 19.6 21 13.6 Z'
  },
  {
    key: 'hals',
    label: 'Hals (brachiocephalicus)',
    d:
      'M21 13.6 C27 19.6 30 29.6 29.6 40.2 L31 43.4 L27.6 41.5 C27.8 39 28.2 37.5 28.5 36.5 ' +
      'C27.6 33 25 29 22.6 25.3 Z'
  },
  {
    key: 'manke',
    label: 'Manke',
    d: 'M47 21 C50 22.75 54 23.75 58.4 24.1 L58.4 34.75 L49.6 28.4 L47.2 24 Z'
  },
  {
    key: 'skulderblad',
    label: 'Skulderblad',
    d:
      'M47.2 24 L49.6 28.4 C49.6 33.2 48.6 38 46.8 42.6 L35.4 45.4 L31 43.4 ' +
      'C36 36.5 42 29.5 47.2 24 Z'
  },
  {
    key: 'overarm',
    label: 'Överarm (triceps)',
    d: 'M46.8 42.6 C45.05 47.15 42.5 51.5 39.4 55.4 L39.2 55.4 C38.8 51.6 37.6 48.4 35.4 45.4 Z'
  },
  {
    key: 'brost',
    label: 'Bröst (pectoralis)',
    d:
      'M27.6 41.5 L31 43.4 L35.4 45.4 C37.6 48.4 38.8 51.6 39.2 55.4 L39 55.5 L34.5 57 L29.5 53 ' +
      'C28.5 49 27.5 45 27.6 41.5 Z'
  },
  {
    key: 'rygg',
    label: 'Rygg (longissimus)',
    d: 'M58.4 24.1 C62.75 24.375 67.5 24 72 23 L72.5 25.4 C67.75 28.7 63 31.85 58.4 34.75 Z'
  },
  {
    key: 'land',
    label: 'Länd (iliocostalis)',
    d: 'M72 23 C74.5 22.7 77.25 22.45 80 22.5 L79.2 23.4 C78.42 25.98 77.71 28.78 77.1 31.7 L72.5 25.4 Z'
  },
  {
    key: 'revben',
    label: 'Revben',
    d:
      'M72.5 25.4 L77.1 31.7 L76.1 37.8 L75.5 45.8 L75.4 48.4 ' +
      'Q72.2 50.4 68.4 46.8 Q64.6 50 60.8 46.2 Q57 49.6 53.2 45.6 Q49.4 48.8 45.6 44.6 ' +
      'L45 42.6 L46.8 42.6 C48.6 38 49.6 33.2 49.6 28.4 L58.4 34.75 C63 31.85 67.75 28.7 72.5 25.4 Z'
  },
  {
    key: 'buk',
    label: 'Buk',
    d:
      'M45.6 44.6 Q49.4 48.8 53.2 45.6 Q57 49.6 60.8 46.2 Q64.6 50 68.4 46.8 Q72.2 50.4 75.4 48.4 ' +
      'L75.6 54 L75.5 55.5 C72 58 65 61 58 61.2 C51 61.4 45 59.5 39 55.5 L39.4 55.4 ' +
      'C42.5 51.5 45.05 47.15 46.8 42.6 L45 42.6 Z'
  },
  {
    key: 'flank',
    label: 'Flank (tensor fasciae latae)',
    d:
      'M79.2 23.4 L80.4 26.2 C83.6 33 85 40 82.6 52.4 L78.8 55 L75.6 54 ' +
      'C74.8 43 76.6 32 79.2 23.4 Z'
  },
  {
    key: 'kors',
    label: 'Kors (gluteus)',
    d:
      'M79.2 23.4 L80 22.5 C82.75 22.6 85.5 23 88 24 C90.5 25 92 26.5 92.5 27.5 L91.2 27.6 ' +
      'C92.4 30.96 93.06 34.9 93.2 39.1 L88.8 40.4 L83.8 39.8 C83.59 34.43 82.32 30.28 80.4 26.2 Z'
  },
  {
    key: 'svansrot',
    label: 'Svansrot',
    d: 'M92.5 27.5 C94.6 29.25 95.97 32.72 96.6 37.4 L93.2 39.1 C93.06 34.9 92.4 30.96 91.2 27.6 Z'
  },
  {
    key: 'lar',
    label: 'Lår (biceps femoris)',
    d:
      'M83.8 39.8 L88.8 40.4 L93.2 39.1 L96.6 37.4 C96.87 39.38 97 41.6 97 44 C97 48 95.5 52 92 59 ' +
      'L90.4 58.6 L86.2 60.4 L80 60.6 L79 62.5 C78 60 76.5 57.5 75.5 55.5 L75.6 54 L78.8 55 L82.6 52.4 ' +
      'C83.56 47.44 83.91 43.34 83.8 39.8 Z'
  },
  {
    key: 'underben-bak',
    label: 'Underben bak',
    d:
      'M80 60.6 L86.2 60.4 L90.4 58.6 L92 59 C90 62.5 88.5 65 88 67.5 L88 68.2 L81.4 68.2 ' +
      'L80.4 65 L79 62.5 Z'
  },
  {
    key: 'bakben',
    label: 'Bakben',
    d:
      'M81.4 68.2 L88 68.2 C87 70.5 86.6 74 86.6 78 C86.6 82 86.5 84 86.5 85.5 C87.5 88 88.2 91 88 94 ' +
      'L81.5 94 C81.5 91.5 82 89 82.8 86 C83 82 82.8 76 82.6 72 Z'
  },
  {
    key: 'framben-under',
    label: 'Underarm',
    d:
      'M29.5 53 L34.5 57 L39 55.5 C38.8 59 38.5 64 38.2 68 C38 70 37.6 72 37.4 73 L37.4 73.4 L33.4 73.4 ' +
      'L33.4 73.5 C33 70 32.4 66 31.6 61.5 C31 58 30 55.5 29.5 53 Z'
  },
  {
    key: 'framben',
    label: 'Framben',
    d:
      'M33.4 73.4 L37.4 73.4 C37.2 77 37 82 36.8 85.5 C37.5 88 38 91 38 94 L31.5 94 ' +
      'C31.5 91 32.4 88 33.3 85.5 C33.4 82 33.4 77 33.4 73.5 Z'
  }
];

const round = (n: number): number => +n.toFixed(2);

interface PathSegment {
  cmd: string;
  nums: number[];
}

function parsePath(d: string): PathSegment[] {
  const segments: PathSegment[] = [];
  const re = /([MLCQZ])([^MLCQZ]*)/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(d)) !== null) {
    const nums = m[2]
      .trim()
      .split(/[\s,]+/)
      .filter(Boolean)
      .map(Number);
    segments.push({ cmd: m[1], nums });
  }
  return segments;
}

/** Mirror every x-coordinate of an absolute M/L/C/Q/Z path across the viewBox width. */
function mirrorPath(d: string): string {
  return parsePath(d)
    .map(({ cmd, nums }) => {
      const mirrored = nums.map((n, i) => (i % 2 === 0 ? round(VIEW_WIDTH - n) : n));
      return mirrored.length ? `${cmd}${mirrored.join(' ')}` : cmd;
    })
    .join(' ');
}

/** Flatten an absolute M/L/C/Q/Z path into polygon points. */
function flattenPath(d: string, steps = 6): string {
  const pts: [number, number][] = [];
  let cx = 0;
  let cy = 0;
  const push = (x: number, y: number) => {
    pts.push([round(x), round(y)]);
    cx = x;
    cy = y;
  };

  for (const { cmd, nums } of parsePath(d)) {
    switch (cmd) {
      case 'M':
      case 'L':
        push(nums[0], nums[1]);
        break;
      case 'C': {
        const [x1, y1, x2, y2, x3, y3] = nums;
        const [x0, y0] = [cx, cy];
        for (let i = 1; i <= steps; i++) {
          const t = i / steps;
          const u = 1 - t;
          push(
            u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
            u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3
          );
        }
        break;
      }
      case 'Q': {
        const [x1, y1, x2, y2] = nums;
        const [x0, y0] = [cx, cy];
        for (let i = 1; i <= steps; i++) {
          const t = i / steps;
          const u = 1 - t;
          push(u * u * x0 + 2 * u * t * x1 + t * t * x2, u * u * y0 + 2 * u * t * y1 + t * t * y2);
        }
        break;
      }
      default:
        break;
    }
  }
  return pts.map(([x, y]) => `${x},${y}`).join(' ');
}

function horseSilhouettes(mirror: boolean): AnatomySilhouette[] {
  const path = (d: string) => (mirror ? mirrorPath(d) : d);
  return [
    ...BACKGROUND_PARTS.map(d => ({ d: path(d), fill: SHADE, stroke: INK, strokeWidth: 0.5 })),
    { d: path(BODY_OUTLINE), fill: BODY, stroke: INK, strokeWidth: 0.6 },
    { d: path(EYE), fill: INK, stroke: 'none', strokeWidth: 0 },
    ...MUSCLE_LINES.map(d => ({ d: path(d), fill: 'none', stroke: INK, strokeWidth: 0.38 }))
  ];
}

function regionsFor(side: AnatomySide): AnatomyRegion[] {
  const mirror = side === 'R';
  return LEFT_REGIONS.map(r => ({
    id: `${r.key}-${side}`,
    label: r.label,
    side,
    points: flattenPath(mirror ? mirrorPath(r.d) : r.d)
  }));
}

export const HORSE_MUSCLES_STANDARD: AnatomyPreset = {
  id: 'horse-muscles-standard',
  label: 'Häst — muskler (standard)',
  viewBox: { width: VIEW_WIDTH, height: VIEW_HEIGHT },
  silhouettes: [...horseSilhouettes(false), ...horseSilhouettes(true)],
  sideLabels: [
    { text: 'Vänster', x: 47, y: 99.2, anchor: 'middle' },
    { text: 'Höger', x: VIEW_WIDTH - 47, y: 99.2, anchor: 'middle' }
  ],
  regions: [...regionsFor('L'), ...regionsFor('R')]
};

export const ANATOMY_PRESETS: Record<string, AnatomyPreset> = {
  [HORSE_MUSCLES_STANDARD.id]: HORSE_MUSCLES_STANDARD
};

export function getAnatomyPreset(id?: string | null): AnatomyPreset {
  if (id && ANATOMY_PRESETS[id]) return ANATOMY_PRESETS[id];
  return HORSE_MUSCLES_STANDARD;
}
