# M5 — Polish & Reports

**Goal:** make the system pleasant to use daily, and make the data it holds useful. This is where accumulated friction gets removed.
**Estimated effort:** 2 weeks
**Prerequisites:** M0–M4 complete and in real use. Read `00-CONTEXT.md`.

> Before writing any code in this milestone, spend one day watching the practitioner use the system for a full working day and write down every hesitation, mis-tap and workaround. That list outranks everything written below.

---

## Scope

### In
Mobile refinement, offline resilience, search, reports, follow-up management, client magic-link portal, audit log UI, accessibility, performance.

### Out
Invoicing and payments. Route optimisation. Multi-practitioner scheduling. These are post-go-live phases.

---

## Tasks

### 1. Mobile and field usability

Everything below is judged on a phone, in a barn, with cold hands and gloves half off.

- Audit every screen at 390px. No horizontal scrolling anywhere.
- Minimum 44×44px tap targets throughout.
- Bottom-anchored primary actions so they are thumb-reachable.
- Numeric keyboards for numeric fields; correct `autocomplete` and `inputmode` on every input.
- Voice input support for free-text journal fields (native dictation — just make sure fields don't fight it).
- Tap-to-call and tap-to-navigate on every phone number and address in the app.
- Dark mode, or at minimum a high-contrast readable palette in bright daylight.

### 2. Offline resilience

- Service worker with an app shell cache so the admin app opens without connectivity.
- Today's and tomorrow's schedule cached locally and readable offline.
- Journal drafts queue in IndexedDB and sync on reconnect (extend the M1 implementation to cover attachments — queue uploads with retry).
- A clear, honest sync status indicator. Never pretend something saved when it didn't.
- Conflict handling: if a draft was edited elsewhere, show both and let the practitioner choose. Never silently overwrite.

### 3. Search

One search box, one endpoint, results grouped by type:
- Clients (name, email, phone — trigram)
- Horses (name, identity, owner name)
- Journals (Swedish full-text over the free-text fields)
- Bookings (by date, client, horse)

Recent items and recent searches surfaced when the box is empty. Keyboard shortcut on desktop.

### 4. Reports

Each as a screen with a date-range filter and CSV export:

| Report | Contents |
|---|---|
| **Behandlingar per period** | Count and revenue estimate by treatment type, by month |
| **Intäktsöversikt** | Completed bookings × captured price, excl. and incl. VAT, by month |
| **Uppföljningar** | Horses whose last treatment plus the treatment's `follow_up_interval` has passed, with days overdue |
| **Osignerade journaler** | Drafts older than 24 h, oldest first |
| **Uteblivna besök** | No-shows and late cancellations by client, to spot patterns |
| **Klientaktivitet** | Clients with no booking in N months (dormant list) |
| **Bokningskällor** | Widget vs manual vs phone, by month |

Keep these as straightforward SQL-backed queries. Do not build a BI layer.

### 5. Follow-up workflow

- The **Uppföljningar** list gets an action: "Skicka påminnelse" which queues the follow-up notification from M4 with a booking link.
- Snooze a follow-up by N weeks, or dismiss with a reason.
- A per-horse follow-up override so a specific horse can be on a different interval than the treatment default.

### 6. Client portal (magic link)

Passwordless access for clients — they book two or three times a year and will forget any password.

- `POST /api/public/auth/magic-link` — accepts an email, always returns 200 regardless of whether the account exists (no enumeration), sends a signed single-use token valid for 15 minutes.
- Token exchange issues a short-lived session scoped to `Client` role and that owner's data only.
- Portal screens: my horses, my upcoming bookings, my booking history, book again, update contact details.
- **Decision required from the practitioner:** do clients see their horse's journal? Default to **no** in v1. If yes, expose a separate, simplified "behandlingssammanfattning" (treatment summary and home-care advice) rather than the full clinical journal — and make it an explicit per-treatment-type setting.
- Rate limit magic-link requests per email and per IP.

### 7. Audit log UI

- A filterable view of `audit_log`: by actor, entity type, entity, date range, action.
- Per-entity history on the horse, booking and journal detail screens ("Visa historik").
- Export a date range to CSV — useful if an inspection ever asks who accessed what.
- Read-only. No UI can delete or edit audit rows, and the database user the app connects as should not have `DELETE` on that table.

### 8. Accessibility

- WCAG 2.2 AA on the widget (public-facing, highest priority) and best-effort on the admin app.
- Full keyboard navigation, visible focus indicators, correct heading hierarchy, form labels and error associations.
- Test with a screen reader in Swedish.
- Colour is never the only carrier of meaning — treatment-type colours need a text label too.

### 9. Performance

Targets, measured on a mid-range Android over 4G:
- Dagens schema interactive in under 1.5 s
- Calendar week view renders 100 bookings without jank
- Slot query responds in under 300 ms for a two-week range
- Admin app initial bundle under 400 kB gzipped; widget under 200 kB

Add `@defer` on the calendar, journal editor and reports. Virtual scrolling on long lists. Review and fix N+1 queries — turn on EF Core sensitive query logging in staging and read the log.

### 10. Error handling and observability

- Every failure state has a Swedish, human message and a recovery action. No raw error codes shown to users.
- Uptime monitoring on `/health/ready` with alerting.
- Error tracking (Sentry self-hosted or equivalent EU-hosted) with source maps.
- A `/docs/runbooks/` folder covering: restore from backup, rotate provider keys, revoke a magic link, respond to a bounce storm.

---

## Acceptance criteria

- [ ] Every friction point from the observation day is either fixed or explicitly deferred with a reason
- [ ] The admin app opens and shows today's schedule with the network disabled
- [ ] A journal draft with an attachment written offline syncs correctly on reconnect
- [ ] Global search finds a client, a horse and a journal from a single query
- [ ] Every report renders and exports to CSV with correct Swedish number and date formatting
- [ ] A follow-up reminder can be sent from the follow-up list and appears in the notification log
- [ ] A client receives a magic link, signs in, sees only their own horses and bookings, and cannot reach another client's data by changing an id
- [ ] Requesting a magic link for a non-existent email is indistinguishable from an existing one
- [ ] The audit log shows who read a specific journal and when
- [ ] Performance targets met on a real mid-range Android over 4G
- [ ] Widget passes an automated WCAG 2.2 AA audit with no critical issues

## Definition of done

The practitioner can go a full week without a single workaround, note-to-self, or "I'll fix that in the spreadsheet".
