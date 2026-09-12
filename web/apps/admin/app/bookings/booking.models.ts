export interface TravelLeg {
  from: 'home' | 'previous';
  fromLabel: string;
  samePlace: boolean;
  distanceKm?: number | null;
  durationMinutes?: number | null;
}

export interface SchemaHome {
  name: string;
  address: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface SchemaStop {
  id: string;
  visitId: string;
  startsAt: string;
  endsAt: string;
  status: string;
  horseId: string;
  horseName?: string;
  ownerId: string;
  ownerName?: string;
  ownerPhone?: string;
  treatmentTypeId: string;
  treatmentName: string;
  treatmentColour?: string;
  durationMinutes: number;
  clientNote?: string;
  journalEntryId?: string;
  locationId?: string;
  locationName?: string;
  addressStreet?: string;
  addressPostcode?: string;
  addressCity?: string;
  latitude?: number | null;
  longitude?: number | null;
  travel?: TravelLeg | null;
}

export interface SchemaDay {
  home: SchemaHome;
  stops: SchemaStop[];
}

export interface BookingListItem {
  id: string;
  visitId: string;
  startsAt: string;
  endsAt: string;
  visitStartsAt: string;
  visitEndsAt: string;
  status: string;
  source: string;
  horseId: string;
  horseName: string;
  ownerId: string;
  ownerName: string;
  ownerPhone?: string;
  treatmentTypeId: string;
  treatmentName: string;
  treatmentColour?: string;
  durationMinutes: number;
  clientNote?: string;
  internalNote?: string;
  journalEntryId?: string;
  emailVerifiedAt?: string | null;
  publicReference?: string | null;
  locationId?: string;
  locationName?: string;
  addressStreet?: string;
  addressPostcode?: string;
  addressCity?: string;
  latitude?: number;
  longitude?: number;
}

export interface Slot {
  startsAt: string;
  endsAt: string;
}

export interface TreatmentTypeRef {
  id: string;
  name: string;
  durationMinutes: number;
  colour?: string;
  status: string;
}

export interface GeoJsonPolygon {
  type: 'Polygon';
  coordinates: number[][][];
}

export interface ZoneCenter {
  lat: number;
  lng: number;
}

export interface Zone {
  id: string;
  name: string;
  geometryKind?: 'polygon' | 'circle' | null;
  geometry?: GeoJsonPolygon | null;
  center?: ZoneCenter | null;
  radiusKm?: number | null;
  bufferKm: number;
  effectiveGeometry?: GeoJsonPolygon | null;
  travelBufferMinutes: number;
  isFallback: boolean;
}

export interface AvailabilityRule {
  id: string;
  practitionerId: string;
  dayOfWeek: string;
  startTime: string;
  endTime: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  zoneId?: string;
  zoneName?: string;
  active: boolean;
}

export interface TimeOffItem {
  id: string;
  startsAt: string;
  endsAt: string;
  allDay: boolean;
  reason?: string;
  recurringAnnual: boolean;
}

export interface LocationItem {
  id: string;
  type: string;
  name: string;
  addressStreet?: string;
  addressPostcode?: string;
  addressCity?: string;
}
