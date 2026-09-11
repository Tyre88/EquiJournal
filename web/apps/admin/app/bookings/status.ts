const BOOKING: Record<string, string> = {
  Requested: 'Förfrågan',
  Confirmed: 'Bekräftad',
  Completed: 'Klar',
  Cancelled: 'Avbokad',
  NoShow: 'Utebliven'
};

const JOURNAL: Record<string, string> = {
  Draft: 'Utkast',
  Signed: 'Signerad'
};

export function statusClass(status: string): string {
  return `badge badge-${status.toLowerCase()}`;
}

export function statusText(status: string): string {
  return BOOKING[status] ?? JOURNAL[status] ?? status;
}
