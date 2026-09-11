/**
 * Swedish date utilities
 */

/**
 * Get the ISO week number for a given date
 */
export function getIsoWeek(date: Date): number {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  const dayNum = d.getUTCDay() || 7;
  d.setUTCDate(d.getUTCDate() + 4 - dayNum);
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  return Math.ceil((((d.getTime() - yearStart.getTime()) / 86400000) + 1) / 7);
}

/**
 * Format a date in Swedish format (YYYY-MM-DD)
 */
export function formatDateSv(date: Date | string | null | undefined): string {
  if (!date) return '';
  const d = new Date(date);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Format a date-time in Swedish format
 */
export function formatDateTimeSv(date: Date | string | null | undefined): string {
  if (!date) return '';
  const d = new Date(date);
  return `${formatDateSv(d)} ${formatTimeSv(d)}`;
}

/**
 * Format time in Swedish format (HH:mm)
 */
export function formatTimeSv(date: Date): string {
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}

/**
 * Get Swedish day names (Monday-first)
 */
export const swedishDayNames: { short: string[]; long: string[] } = {
  short: ['mån', 'tis', 'ons', 'tor', 'fre', 'lör', 'sön'],
  long: ['måndag', 'tisdag', 'onsdag', 'torsdag', 'fredag', 'lördag', 'söndag']
};

/**
 * Get Swedish month names
 */
export const swedishMonthNames: string[] = [
  'januari', 'februari', 'mars', 'april', 'maj', 'juni',
  'juli', 'augusti', 'september', 'oktober', 'november', 'december'
];

/**
 * Format a date in long Swedish format
 */
export function formatDateLongSv(date: Date): string {
  return `${date.getDate()}. ${swedishMonthNames[date.getMonth()]} ${date.getFullYear()}`;
}
