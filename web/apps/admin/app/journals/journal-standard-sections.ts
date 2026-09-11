export const STANDARD_ANATOMY_SECTIONS = [
  { key: 'undersokningsfynd', label: 'Undersökningsfynd', preset: 'horse-muscles-standard' },
  { key: 'behandling_karta', label: 'Behandling', preset: 'horse-skeleton-standard' }
] as const;

export const STANDARD_ANATOMY_PRESETS = new Set<string>(
  STANDARD_ANATOMY_SECTIONS.map(s => s.preset)
);

export const STANDARD_SECTION_LABELS: Record<string, string> = Object.fromEntries(
  STANDARD_ANATOMY_SECTIONS.map(s => [s.key, s.label])
);
