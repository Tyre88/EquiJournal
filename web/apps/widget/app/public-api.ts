import { environment } from '../environments/environment';

export class PublicApiError extends Error {
  constructor(public status: number, message: string, public slots?: string[]) {
    super(message);
  }
}

export function tenantSlug(): string {
  const path = window.location.pathname.replace(/^\/+|\/+$/g, '');
  const parts = path.split('/').filter(Boolean);
  if (parts[0] === 'widget' || parts[0] === 'portal')
    return parts[1] ?? '';
  return parts[0] ?? '';
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${environment.apiUrl}${path}`;
  let res: Response;
  try {
    res = await fetch(url, {
      ...init,
      headers: {
        'Accept': 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...(init?.headers ?? {})
      }
    });
  } catch {
    throw new PublicApiError(0, 'offline');
  }

  if (res.status === 429) throw new PublicApiError(429, 'rate');
  if (res.status === 409) {
    const body = await res.json().catch(() => ({}));
    throw new PublicApiError(409, body.message ?? 'conflict', body.slots);
  }
  if (res.status === 404) throw new PublicApiError(404, 'notfound');
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new PublicApiError(res.status, body.message ?? 'error');
  }
  if (res.status === 204) return undefined as T;
  return await res.json() as T;
}

export interface PublicTreatment {
  id: string;
  slug: string;
  name: string;
  publicDescription?: string;
  durationMinutes: number;
  requiresApproval: boolean;
  price?: number;
}

export interface TreatmentsResponse {
  treatments: PublicTreatment[];
  bookingTerms: string;
  privacyPolicyUrl: string;
  contactPhone: string;
  contactEmail: string;
  cancellationNoticeHours: number;
}

export interface BookingSummary {
  publicReference: string;
  treatmentName: string;
  startsAt: string;
  endsAt: string;
  status: string;
  addressPostcode?: string;
  addressCity?: string;
  cancellationPolicy?: string | null;
}

export const publicApi = {
  treatments: () => request<TreatmentsResponse>(`/api/public/${tenantSlug()}/treatments`),
  slots: (treatmentId: string, from: string, to: string, postcode: string) =>
    request<{ starts: string[] }>(`/api/public/${tenantSlug()}/slots?treatmentId=${treatmentId}&from=${from}&to=${to}&postcode=${encodeURIComponent(postcode)}`),
  create: (body: unknown) => request<{ reference: string }>(`/api/public/${tenantSlug()}/bookings`, { method: 'POST', body: JSON.stringify(body) }),
  verify: (token: string) => request<void>('/api/public/bookings/verify', { method: 'POST', body: JSON.stringify({ token }) }),
  summary: (token: string) => request<BookingSummary>(`/api/public/bookings/${encodeURIComponent(token)}`),
  cancel: (token: string) => request<void>(`/api/public/bookings/${encodeURIComponent(token)}/cancel`, { method: 'POST', body: JSON.stringify({}) }),
  reschedule: (token: string, startsAt: string) =>
    request<void>(`/api/public/bookings/${encodeURIComponent(token)}/reschedule`, { method: 'POST', body: JSON.stringify({ startsAt }) })
};
