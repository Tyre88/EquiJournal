# M2 — Internal Booking

**Goal:** the practitioner can manage availability, see and manipulate a calendar, book treatments manually, and turn a completed booking into a journal draft in one tap.
**Estimated effort:** 3 weeks
**Prerequisites:** M0 and M1 complete, and M1 in real use. Read `00-CONTEXT.md`.

> The slot engine is the hard part of this project. Treat it as a pure, exhaustively unit-tested function. Everything else here is CRUD and UI.

---

## Scope

### In
Availability rules, time off, geographic zones, the slot generation engine, database-level double-booking prevention, manual booking CRUD, calendar UI, booking → journal handoff.

### Out
The public widget and any anonymous access (M3). Notifications and reminders (M4) — M2 sends nothing.

---

## Tasks

### 1. Locations and zones
- `Location`: type (`ClientStable` \| `FixedSite`), address, coordinates, name.
- `Zone`: name, and a definition (list of postcodes is simplest and sufficient — a polygon is over-engineering here).
- Resolve a postcode to a zone. Unmatched postcodes fall into a `Övrigt` zone.

### 2. Availability rules
`AvailabilityRule`: practitioner_id, day of week, start time, end time, effective from/to (nullable), optional zone_id, active.

Example configuration the system must support:
- Mon–Thu 08:00–17:00, Fri 08:00–13:00
- Tuesdays restricted to zone "Sjöbo/Tomelilla", Thursdays to "Lund/Malmö"

`TimeOff`: practitioner_id, start, end, all-day flag, reason, recurring (annual) flag for holidays.

CRUD endpoints and an admin UI for both.

### 3. Booking entity
Fields: id, horse_id, owner_id, treatment_type_id, practitioner_id, location_id, `starts_at`, `ends_at`, denormalised `treatment_name`/`duration_minutes`/`price`/`vat_rate`, status, source (`Widget` \| `Manual` \| `Phone`), client note, internal note, cancellation reason, cancelled_at/by, `created_at`, `updated_at`.

Status machine — enforce transitions in the domain layer, reject anything else:

```
Requested ──approve──► Confirmed ──complete──► Completed
    │                      │
    └──cancel──────────────┴──cancel──► Cancelled
                           └──no-show──► NoShow
```

Every transition writes to `audit_log`.

### 4. Double-booking prevention — in the database

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE bookings
  ADD COLUMN time_range tstzrange
    GENERATED ALWAYS AS (tstzrange(starts_at, ends_at, '[)')) STORED;

ALTER TABLE bookings
  ADD CONSTRAINT bookings_no_overlap
  EXCLUDE USING gist (practitioner_id WITH =, time_range WITH &&)
  WHERE (status IN ('Requested','Confirmed'));
```

The API catches PostgreSQL error `23P01` and returns `409 Conflict` with a fresh list of available slots. **Do not** implement overlap checking in C# as the primary defence — it will race.

Write an integration test that fires two concurrent booking requests for the same slot and asserts exactly one succeeds.

### 5. Slot generation engine

Signature roughly:

```csharp
IReadOnlyList<Slot> GetAvailableSlots(
    Guid practitionerId,
    Guid treatmentTypeId,
    LocalDate from, LocalDate to,
    string? postcode,
    SlotOptions options);
```

Algorithm — implement in this order:

1. Expand `AvailabilityRule` into concrete windows **in `Europe/Stockholm` local time**, then convert to UTC. Never expand in UTC — DST transitions will silently corrupt the schedule twice a year.
2. Subtract `TimeOff`.
3. Subtract existing bookings with status `Requested` or `Confirmed`, each padded by the treatment's buffer before and after.
4. If zone-restricted days are configured and a postcode was supplied, drop days whose zone doesn't match.
5. Apply a travel buffer: a flat per-zone value in v1. Leave a seam for distance-based buffers later, but do not build them now.
6. Slice remaining windows into candidate starts at a configurable granularity (default 30 min) that fit `duration + buffers`.
7. Apply `min_notice_hours` and `max_advance_days` from the treatment type.
8. Cap the number of returned slots.

**Rules:**
- Pure function of its inputs — no direct DB access inside the core; feed it loaded data. This makes it exhaustively unit-testable.
- Slots are **computed on demand and never materialised** into a table. Cache per `(practitioner, date, treatment type, zone)` with a short TTL; invalidate on any booking, rule or time-off change.
- Use NodaTime (`Npgsql.NodaTime`) for the calendar arithmetic if the timezone handling gets awkward — it will.

**Required unit tests:**
- A booking blocks the slots its buffers cover, not just its duration
- The spring-forward DST Sunday produces no phantom 02:00 slot
- The autumn fall-back Sunday does not produce duplicate slots
- Min notice excludes slots too soon from now
- Zone-restricted days exclude the wrong postcode
- A treatment longer than the remaining window at end of day is not offered
- An all-day time-off removes the whole day

### 6. Booking endpoints (`/api/app/*`)
- `GET /bookings` — filter by date range, status, practitioner, horse, owner
- `GET /bookings/{id}`
- `POST /bookings` — manual creation; bypasses min-notice and online-bookable restrictions (you can book anything, anytime)
- `PATCH /bookings/{id}` — reschedule, change notes
- `POST /bookings/{id}/{approve|cancel|complete|no-show}`
- `GET /availability/slots` — internal version of the slot query

### 7. Booking → journal handoff
`POST /bookings/{id}/complete` sets status `Completed` and **creates a journal draft** pre-filled with:
- horse, owner snapshot, species, sex, age, identity
- `performed_at` = booking start time
- treatment type and its journal template
- the client's booking note copied into a suggested `anamnes` starting point

Return the new journal id so the UI navigates straight into the editor. One tap from "done" to "writing the journal" — this is what makes the legal requirement to journal *in direct connection to the visit* actually achievable.

### 8. Admin UI

1. **Dagens schema** — the app's default landing page. Chronological list for today: time, client name, phone (tap to call), stable address (tap to open in maps), horse name, treatment, note. Each row has a one-tap **Klar → journal** action. This screen is used in the car; optimise it ruthlessly.
2. **Kalender** — week and month views. Colour by treatment type. Drag to reschedule (with conflict feedback from the 409). Click empty space to create a booking.
3. **Bokningsdetalj** — full details, status transitions, notes, link to the horse's journal history.
4. **Ny bokning** — search or create client and horse inline, pick treatment, pick slot from the availability engine or override with a free time, confirm.
5. **Tillgänglighet** — availability rules, zones, time off. A preview pane showing generated slots for the coming two weeks so the effect of a rule change is visible immediately.

### 9. Open decision to resolve before building
**Group visits:** if several horses at the same stable are treated in one visit, decide now whether a `Booking` covers one horse or a visit covers many. Retrofitting this later is expensive. Recommended model if group visits happen: a `Visit` holds time and location, and contains one or more `BookingLine` rows, each with a horse and treatment type; each line produces its own journal entry. **Ask the practitioner before implementing.**

---

## Acceptance criteria

- [ ] Availability rules produce correct slots across a spring-forward and a fall-back weekend — verified by unit test
- [ ] Two concurrent requests for the same slot: exactly one succeeds, the other gets 409 with fresh slots
- [ ] A booking's buffers block adjacent slots
- [ ] Zone-restricted days offer slots only to matching postcodes
- [ ] A booking can be dragged to a new time in the calendar and the change persists
- [ ] Completing a booking creates a pre-filled journal draft and navigates to it
- [ ] Cancelling a booking frees its slot immediately
- [ ] Changing an availability rule invalidates the slot cache
- [ ] Dagens schema loads in under one second on a phone over 4G and is usable one-handed
- [ ] All booking state transitions appear in `audit_log`

## Definition of done

The practitioner runs their real schedule from this system for at least two weeks, booking manually, with no parallel paper or spreadsheet calendar. Only then does M3 begin.
