# 00 — Shared Context (read this before any milestone)

You are building **HästJournal** (working name): a booking and journal system for equine treatments, used by a single practitioner in Skåne, Sweden, who travels to clients' stables.

## Stack — non-negotiable

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs |
| ORM | EF Core 10 + Npgsql |
| Frontend | Angular 20+, standalone components, signals, zoneless |
| Database | PostgreSQL 16+ |
| Hosting target | Linux container, EU region |
| UI language | Swedish (`sv-SE`) |
| Timezone | Store UTC (`timestamptz`), display `Europe/Stockholm` |

## Repository layout

```
/src
  Equine.Api/              ASP.NET Core host, Minimal API endpoints (feature folders)
    Features/{Bookings,Journals,Horses,Owners,TreatmentTypes,Availability,Admin}
  Equine.Domain/           entities, value objects, domain rules — no EF dependency
  Equine.Infrastructure/   DbContext, migrations, storage, email, SMS
  Equine.Jobs/             scheduled background work
/tests
  Equine.UnitTests/
  Equine.IntegrationTests/ Testcontainers + real PostgreSQL
/web                       Angular workspace
  apps/admin/              authenticated practitioner app
  apps/widget/             public anonymous booking app
  libs/api-client/         generated from OpenAPI — never hand-written
  libs/ui/                 shared components, sv-SE locale, date helpers
/widget-loader             tiny vanilla TS loader script (<5 kB gzipped)
/docs                      ADRs, runbooks, retention policy
```

## Architecture rules

1. **Modular monolith.** One deployable API. Feature folders, not microservices.
2. **No MediatR, no CQRS ceremony.** Plain handler classes registered in DI.
3. **Two API surfaces, strictly separated — this is a security boundary:**
   - `/api/public/*` — anonymous, rate-limited, narrow. Never returns owner names, horse names, or the reason a slot is unavailable.
   - `/api/app/*` — authenticated, everything else.
4. **Server-side ownership scoping always.** Never rely on the client to filter data it shouldn't see.
5. **UUID v7 primary keys** (`Guid.CreateVersion7()`).
6. **`citext`** for emails.
7. **Soft delete** (`deleted_at`) on owners, horses, treatment types. **No delete at all** on journals or amendments.
8. Every booking state transition and every journal signature is written to `audit_log`.

## Regulatory constraints (Swedish law — these drive the design)

Under lagen (2009:302) and SJVFS 2023:19, journalling is a legal obligation:

- Journal written **in direct connection** to the consultation.
- Journal in **Swedish** (Norwegian/Danish also permitted).
- **Corrections must be dated and signed** by whoever makes them → journals are **append-only**; signed entries are immutable; changes become amendments.
- Must be possible to find consultations for a **specific animal at a given time**.
- Journals and underlying documents kept **at least 5 years**.
- Required content: owner name/address/phone, species (*djurslag*), sex, age or age group, animal identity, date, time, anamnes, status, examinations/treatments performed **and why**, diagnosis and differential diagnoses, prognosis and plan including home-care advice.

**GDPR:** the journal contains the owner's personal data but is retained under *legal obligation*, which overrides erasure requests for the journal itself. Account data, booking metadata and marketing consent are separate and **are** erasable. Keep them separable in the model.

## Domain model summary

```
Owner ──1:N── Horse ──1:N── JournalEntry ──1:N── JournalAmendment
  │             │                │
  │             │                └──1:N── Attachment
  └──1:N── Booking ──┬── TreatmentType (N:1) ──1:1── JournalTemplate
                     ├── Location (N:1)
                     └── Practitioner (N:1)
Practitioner ──1:N── AvailabilityRule, TimeOff
```

## Conventions

- **Backend:** `DateTimeOffset` in code, `timestamptz` in DB. FluentValidation for requests. Serilog structured logging. OpenAPI generated from endpoint metadata.
- **Frontend:** standalone components only, no NgModules. Signals for state. New control flow (`@if`, `@for`, `@defer`). Typed reactive forms. `LOCALE_ID = 'sv-SE'`, Monday-first weeks, ISO week numbers.
- **Tests:** unit tests for domain rules and the slot engine; integration tests against real PostgreSQL via Testcontainers. Every milestone ships with its tests, not after.
- **Migrations:** EF Core migrations, applied by an explicit startup step or a separate migration container.
- **Commits:** conventional commits. No branch lives longer than a week.

## Working agreement for the AI agent

- Ask before inventing a requirement. If a milestone is ambiguous, list the ambiguity and propose a default rather than silently choosing.
- Do not add libraries beyond those named without saying why.
- Do not scaffold features from later milestones. Stay in scope.
- Produce working, tested code — not placeholders or `TODO` stubs.
- After each task, state what changed and what the next verifiable step is.

## Milestone order

M0 Foundations → M1 Records & Journals → M2 Internal Booking → M3 Public Widget →
M4 Notifications → M5 Polish & Reports → M6 Go Live

**M1 and M2 go into real production use before M3 starts.**
