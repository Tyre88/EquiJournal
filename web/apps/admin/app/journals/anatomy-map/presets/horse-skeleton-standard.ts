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
 * Same two-horse layout as the muscle preset, with simplified skeletal
 * contours (skull, spine, ribs, pelvis, limb bones) instead of musculature.
 */
const BONE_LINES: string[] = [
  // skull: vault, orbit, nasal, mandible
  'M18.5 10 C16 12 11.5 14.5 7.5 19 C5.8 21.2 4.4 24 4 26.4',
  'M18.5 10 C20.2 14.2 19.6 18.4 16.2 22.4',
  'M8.2 18.6 C10.2 20.6 11.4 23.4 10.6 26.6',
  'M4 26.4 C6.2 28.4 10.4 29.2 15.2 28.2 C18.2 27.5 20.8 26.2 22.6 25.3',
  'M12.2 15.2 C12.8 14.4 14.4 14.4 14.8 15.6',
  // cervical spine
  'M22 11 C29 13 37 17 45 22.4',
  'M23.6 16.2 C30 18.6 37 22.4 43.4 26.8',
  // thoracic + lumbar spine
  'M47 21 C55 23.2 64 23.8 72 23',
  'M72 23 C76.2 22.5 79.2 22.5 82.2 23.4',
  // sacrum / caudal vertebrae
  'M82.2 23.4 C86.4 24.8 90.2 27 93.2 29.4',
  'M93.2 29.4 C95.6 32.4 97 38 96.4 44',
  // scapula
  'M47.2 24 C43 30.2 38 36.4 32.6 43.2',
  'M47.2 24 C49.2 28.4 48.6 34.2 46 40.4',
  // humerus → radius/ulna → cannon → hoof
  'M32.6 43.2 C35.2 47.2 37.6 51.4 39 55.4',
  'M39 55.4 C37.4 61.2 36.4 67.2 35.8 73.2',
  'M35.8 73.2 C36 78.2 36.1 82.2 36 86.2',
  'M34.2 86.2 C35.2 86.2 36.6 86.2 37.2 86.5',
  // ribs
  'M50 25 C52 32.2 53.2 40 52.6 48.2',
  'M55 24.6 C57.6 32.2 58.8 40.2 58.4 50.4',
  'M60 24.3 C63.2 32.2 64.6 40.4 64 52.2',
  'M65 24 C68.4 32 70 40.2 69.4 52',
  'M70 23.6 C73.2 31.2 74.6 39.2 73.8 50',
  'M50.2 44 C56.4 48.2 64.2 50.2 72 48',
  // pelvis
  'M80 23 C78.2 30.2 77.2 38.2 77.6 46.2',
  'M80 23 C85.2 25.2 90 28.2 93.6 32.2',
  'M82 32.4 C86.2 34.4 90.2 36.4 93.2 38.4',
  'M77.6 46.2 C82.2 48.2 88.2 48.2 93.2 44.2',
  // femur → tibia → cannon → hoof
  'M82.2 38.2 C81.2 45.2 80.6 52.4 80.2 60.2',
  'M80.2 60.2 C83.2 63.2 86 65.6 87.8 68.2',
  'M87.8 68.2 C86.4 74.2 85.8 80.2 85.6 86.2',
  'M83.6 86.4 C85 86.2 86.4 86.2 87 86.6',
  // sternum
  'M31.2 44 C34.2 48.2 36.2 52 37.2 55.2'
];

function horseSilhouettes(mirror: boolean): AnatomySilhouette[] {
  const path = (d: string) => (mirror ? mirrorPath(d) : d);
  return [
    ...BACKGROUND_PARTS.map(d => ({ d: path(d), fill: SHADE, stroke: INK, strokeWidth: 0.5 })),
    { d: path(BODY_OUTLINE), fill: BODY, stroke: INK, strokeWidth: 0.6 },
    { d: path(EYE), fill: INK, stroke: 'none', strokeWidth: 0 },
    ...BONE_LINES.map(d => ({ d: path(d), fill: 'none', stroke: INK, strokeWidth: 0.42 }))
  ];
}

export const HORSE_SKELETON_STANDARD: AnatomyPreset = {
  id: 'horse-skeleton-standard',
  label: 'Häst — skelett (standard)',
  viewBox: { width: VIEW_WIDTH, height: VIEW_HEIGHT },
  silhouettes: [...horseSilhouettes(false), ...horseSilhouettes(true)],
  sideLabels: HORSE_SIDE_LABELS,
  regions: [...regionsFor('L'), ...regionsFor('R')]
};
