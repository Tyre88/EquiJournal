import { AnatomyPreset } from '../anatomy-map.types';
import { HORSE_MUSCLES_STANDARD } from './horse-muscles-standard';
import { HORSE_SKELETON_STANDARD } from './horse-skeleton-standard';

export const ANATOMY_PRESETS: Record<string, AnatomyPreset> = {
  [HORSE_MUSCLES_STANDARD.id]: HORSE_MUSCLES_STANDARD,
  [HORSE_SKELETON_STANDARD.id]: HORSE_SKELETON_STANDARD
};

export function getAnatomyPreset(id?: string | null): AnatomyPreset {
  if (id && ANATOMY_PRESETS[id]) return ANATOMY_PRESETS[id];
  return HORSE_MUSCLES_STANDARD;
}

export { HORSE_MUSCLES_STANDARD, HORSE_SKELETON_STANDARD };
