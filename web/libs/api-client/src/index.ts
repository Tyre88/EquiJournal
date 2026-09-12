// Typed API surface used by the admin app.
// Regenerate with `npm run generate:api` against http://localhost:5087/swagger/v1/swagger.json

export interface OwnerDto {
  id: string;
  name: string;
  email: string;
  phone?: string;
  addressCity?: string;
}

export interface HorseDto {
  id: string;
  name: string;
  ownerName?: string;
  status?: string;
}

export interface JournalDto {
  id: string;
  horseId: string;
  status: 'Draft' | 'Signed';
  performedAt: string;
}

export interface TenantDto {
  id: string;
  name: string;
  slug: string;
  plan: string;
}

export const appApiPaths = {
  owners: '/api/app/owners',
  horses: '/api/app/horses',
  journals: '/api/app/journals',
  treatmentTypes: '/api/app/treatment-types',
  me: '/api/app/me'
} as const;

export const publicApiPaths = {
  register: '/api/public/tenants/register',
  treatments: (slug: string) => `/api/public/${slug}/treatments`,
  slots: (slug: string) => `/api/public/${slug}/slots`,
  bookings: (slug: string) => `/api/public/${slug}/bookings`,
  magicLink: (slug: string) => `/api/public/${slug}/auth/magic-link`
} as const;
