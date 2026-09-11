import { AnatomyPreset, AnatomySilhouette } from '../anatomy-map.types';
import {
  BACKGROUND_PARTS,
  BODY,
  BODY_OUTLINE,
  EYE,
  HORSE_SIDE_LABELS,
  INK,
  SHADE,
  VIEW_HEIGHT,
  VIEW_WIDTH,
  mirrorPath,
  regionsFor
} from './horse-body-geometry';

/**
 * Two side-view horses with superficial musculature, drawn side by side like a
 * printed palpation sheet: the left horse faces left (showing its left side),
 * the right horse is a mirror image facing right (showing its right side).
 */
const MUSCLE_LINES: string[] = [
  'M4.4 25 C5.4 24.2 6 25 5.6 26',
  'M3.6 28.4 C5 28.2 6.2 28.2 7.2 28.2',
  'M16.4 13.8 C20.8 19 20 26.6 15 29',
  'M10.8 19.4 C12.6 21.2 13.2 24.2 12.2 27.4',
  'M18 11.4 C18.8 13.4 19.6 15.4 19.4 17.2',
  'M24.2 11.6 C32 13 41 17.4 46.6 24.6',
  'M21 13.6 C27 19.6 30 29.6 29.6 40.2',
  'M23.4 26 C25.8 30 27.6 34.6 28.4 39.4',
  'M27.4 15.2 C32.6 18.4 38 23.6 42 29.2',
  'M38 13 C40.6 18 43.2 22.6 44.6 26.8',
  'M47.2 24 C42 29.5 36 36.5 31 43.4',
  'M49.6 28.4 C49.6 38 45.6 47.6 39.4 55.4',
  'M29.6 45 C33 48 36.6 51.6 39.2 55.4',
  'M35.4 45.4 C37.6 48.4 38.8 51.6 39.2 55.4',
  'M72.5 25.4 C63 32 53.5 38 45 42.6',
  'M45.6 44.6 Q49.4 48.8 53.2 45.6 Q57 49.6 60.8 46.2 Q64.6 50 68.4 46.8 Q72.2 50.4 75.4 48.4',
  'M50.4 49 C51.6 52 52.6 55 53.2 58.6',
  'M56.4 49.4 C57.6 52.4 58.6 55.6 59.2 59',
  'M62.4 49.6 C63.6 52.6 64.6 55.6 65.2 59',
  'M68.4 50 C69.4 52.6 70.2 55 70.8 57.6',
  'M44.6 52.4 C52 56.6 62 57.8 72.6 54.4',
  'M79.2 23.4 C76.6 32 74.8 43 75.6 54',
  'M80.4 26.2 C83.6 33 85 40 82.6 52.4',
  'M81 24.2 C85 26 89 28.4 93 31',
  'M86.6 24.8 C89.6 36 89.6 48 86.2 60.4',
  'M91.2 27.6 C94.2 36 93.8 48 90.4 58.6',
  'M76.2 53 C77.8 52.4 78.8 53.4 78.8 55',
  'M80 60.6 C83 62.6 85.6 65 87.4 67.4',
  'M34.6 60 C35.2 64 35.6 68 35.6 72',
  'M33.4 73.4 L37.4 73.4',
  'M36.2 75.5 C36.4 79 36.4 83 36.2 85.5',
  'M82.8 68.2 L88 68.2',
  'M85.6 70.5 C85.7 75 85.8 80 85.6 85',
  'M33.3 86.4 C35 86 36 86 36.8 86.4',
  'M82.8 86.6 C84.5 86.2 85.5 86.2 86.5 86.6'
];

function horseSilhouettes(mirror: boolean): AnatomySilhouette[] {
  const path = (d: string) => (mirror ? mirrorPath(d) : d);
  return [
    ...BACKGROUND_PARTS.map(d => ({ d: path(d), fill: SHADE, stroke: INK, strokeWidth: 0.5 })),
    { d: path(BODY_OUTLINE), fill: BODY, stroke: INK, strokeWidth: 0.6 },
    { d: path(EYE), fill: INK, stroke: 'none', strokeWidth: 0 },
    ...MUSCLE_LINES.map(d => ({ d: path(d), fill: 'none', stroke: INK, strokeWidth: 0.38 }))
  ];
}

export const HORSE_MUSCLES_STANDARD: AnatomyPreset = {
  id: 'horse-muscles-standard',
  label: 'Häst — muskler (standard)',
  viewBox: { width: VIEW_WIDTH, height: VIEW_HEIGHT },
  silhouettes: [...horseSilhouettes(false), ...horseSilhouettes(true)],
  sideLabels: HORSE_SIDE_LABELS,
  regions: [...regionsFor('L'), ...regionsFor('R')]
};
