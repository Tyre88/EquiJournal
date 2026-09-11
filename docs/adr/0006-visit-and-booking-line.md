# ADR-0006: Visit and BookingLine

**Status:** Accepted  
**Date:** 2026-09-10  
**Subject:** Group visits use a Visit (time + location) containing one or more BookingLine rows

## Context

A travelling practitioner often treats several horses at the same stable in one stop. The M2 overlap constraint is per practitioner and time range. If each horse were a standalone booking with its own `starts_at`/`ends_at`, two horses at 10:00 would violate the exclusion constraint.

## Decision

- A **Visit** owns `practitioner_id`, `location_id`, `starts_at`, `ends_at`, and `occupies_slot`.
- PostgreSQL `EXCLUDE USING gist` applies to **visits** where `occupies_slot = true`.
- Each **BookingLine** is one horse + treatment, with its own status machine and journal handoff.
- Line clock times are derived sequentially from the visit start and `sort_order`.
- All lines on a visit share the visit location. A horse at another stable requires a new visit.
- Manual create starts lines as `Confirmed`. The public widget (M3) creates one visit with one `Requested` line.

`occupies_slot` is true iff any line is `Requested` or `Confirmed`. Cancelling or completing the last blocking line frees the slot immediately.

## Consequences

### Positive
- Group visits occupy one calendar block without racing the overlap constraint.
- Dagens schema and journal completion stay per horse.
- M3 stays a single-line visit.

### Negative
- Reschedule moves the whole visit; individual line times are derived, not independently stored.
- Adding a horse from a different stable is rejected instead of silently splitting.
