# M3 — Public Booking Widget

**Goal:** clients can book a treatment from an embedded widget on the practitioner's public website, without an account.
**Estimated effort:** 2 weeks
**Prerequisites:** M0–M2 complete and in real production use for at least two weeks. Read `00-CONTEXT.md`.

> The public API is exposed on every website that embeds this widget. Treat every endpoint in this milestone as hostile-facing.

---

## Scope

### In
The public API surface, the widget Angular app, the loader script, embed configuration, CORS and rate limiting, email verification, cancel/reschedule links.

### Out
Payments and deposits. Client accounts and login (magic links may be deferred to M5 — see below). Notifications beyond the two transactional emails needed to close the booking loop (full notification system is M4).

---

## Tasks

### 1. Public API (`/api/public/*`)

Exactly these endpoints, nothing more:

| Endpoint | Returns |
|---|---|
| `GET /treatments` | Only treatments where `bookable_online = true` and `active = true`. Fields: id, name, public description, duration, price (only if price display is enabled), requires approval. **Nothing else.** |
| `GET /slots?treatmentId=&from=&to=&postcode=` | Free slot start times only |
| `POST /bookings` | Creates a `Requested` booking, returns a booking reference and nothing else |
| `POST /bookings/verify` | Confirms the email verification token |
| `GET /bookings/{token}` | Booking summary for a signed cancel/reschedule link |
| `POST /bookings/{token}/cancel` | Cancels |
| `POST /bookings/{token}/reschedule` | Moves to a new slot |

**Hard rules — write tests that assert each:**
- No endpoint ever returns an owner name, horse name, another booking, or the *reason* a slot is unavailable.
- Slot responses are indistinguishable whether a slot is blocked by an existing booking, time off, or a zone rule.
- `GET /bookings/{token}` requires a signed, expiring, single-purpose token — never a guessable booking id.
- Enumeration is impossible: an unknown token and an expired token return the same response.

### 2. Booking submission flow

`POST /api/public/bookings` accepts: treatment id, slot start, horse details (name, age or age group, breed, any known issues), owner details (name, email, phone, stable address + postcode), free-text note, consent flag.

Server behaviour:
1. Validate the slot is still free; on conflict return 409 with fresh slots.
2. Match the email against existing owners. If matched, link to that owner — **do not** create a duplicate, and **do not** reveal to the caller that a match happened.
3. Create the owner/horse as `Unverified` if new.
4. Create the booking with status `Requested`.
5. Send a verification email with a signed token.
6. On verification: if the treatment has `requires_approval = false`, transition to `Confirmed` and send confirmation with an `.ics` attachment. If `true`, leave as `Requested` and notify the practitioner to approve.

An unverified booking that is not verified within a configurable window (default 30 minutes) is automatically expired and its slot released. Implement this as a background job.

### 3. Hardening

- **CORS:** strict origin allowlist, configurable in the admin panel. Not a wildcard, ever.
- **CSP:** send `frame-ancestors` limited to the same allowlist, so the widget can only be framed by approved sites.
- **Rate limiting:** per-IP fixed window on reads; a much stricter per-IP and per-email limit on `POST /bookings`.
- **Bot protection:** honeypot field plus a minimum time-on-form check first. Add Cloudflare Turnstile only if spam actually appears — do not add friction preemptively.
- Input validation on everything; postcode normalised and validated against Swedish format.
- Log every public booking attempt with IP and outcome.

### 4. Widget app (`apps/widget`)

A separate Angular app, kept deliberately small. Lazy-load aggressively; target a first-load bundle well under 200 kB gzipped.

**Flow:**
1. **Välj behandling** — or skip if pre-selected via `data-treatment`
2. **Var finns hästen?** — postcode → resolves zone, which determines which days are offered
3. **Välj tid** — calendar showing available days, then slots for the chosen day
4. **Hästens uppgifter** — name, age, breed, brief description of the problem
5. **Dina uppgifter** — name, email, phone, stable address
6. **Bekräfta** — summary, consent checkbox linking to booking terms and privacy policy
7. **Klart** — "Kontrollera din e-post för att bekräfta bokningen"

**Requirements:**
- Mobile-first; most bookings will come from a phone.
- Swedish throughout. Clear, plain language.
- Full keyboard navigation and screen-reader labels — this is public-facing.
- Graceful failure: if the API is down, show a phone number and email, not a stack trace.
- No analytics or third-party scripts unless explicitly requested and covered by the privacy policy.

### 5. Loader script (`/widget-loader`)

Vanilla TypeScript, no framework, **under 5 kB gzipped**, built as a standalone IIFE.

```html
<div id="hastbokning"></div>
<script src="https://booking.dindoman.se/widget/v1/loader.js"
        data-target="#hastbokning"
        data-treatment="massage"
        data-lang="sv"
        async></script>
```

Responsibilities:
- Inject an `<iframe>` into the target element with the correct query parameters.
- Listen on `postMessage` for height changes from the widget and resize the iframe. Origin-check every message.
- Handle the case where the target element does not exist (fail silently, log to console).
- Expose nothing on `window` beyond a single namespaced object.

**Why an iframe and not Angular Elements:** complete CSS and JS isolation from unpredictable WordPress themes, no CSP changes needed on the host site, and transparent upgrades. Write this as an ADR. Offer a custom-element build later only if a specific site genuinely needs inline styling control.

### 6. Admin: widget settings

A screen in the admin panel for:
- Allowed embedding origins (drives both CORS and `frame-ancestors`)
- Show prices in the widget (bool)
- Which treatments appear online (already per-treatment, surfaced here as an overview)
- Booking terms text and privacy policy URL
- Verification window minutes
- **Copyable embed snippet**, generated with the current settings

Plus an **inkomna bokningsförfrågningar** view: pending `Requested` bookings from the widget with one-tap approve/decline.

### 7. Cancel and reschedule links

Every confirmation email contains a signed, expiring link. The client can view the booking, cancel it, or move it to another free slot. Cancellation within a configurable notice period shows a message about the cancellation policy (but takes no payment action — that is a later phase).

---

## Acceptance criteria

- [ ] The widget is embedded on a real WordPress test site and renders correctly across three different themes
- [ ] The iframe auto-resizes as the flow progresses, with no scrollbars or clipping
- [ ] Embedding from a non-allowlisted origin fails — both CORS and `frame-ancestors` block it
- [ ] A booking submitted from the widget appears in the practitioner's calendar after email verification
- [ ] An unverified booking expires after the configured window and releases its slot
- [ ] Two clients submitting the same slot simultaneously: one succeeds, one gets fresh slots and a clear message
- [ ] No public endpoint returns any personal data belonging to another client — verified by explicit tests
- [ ] An unknown token and an expired token produce identical responses
- [ ] Rate limits return 429 and the widget shows a sensible message
- [ ] The full flow is completable on a 390px phone in under 90 seconds
- [ ] The flow is completable using only a keyboard, with correct screen-reader labels
- [ ] Cancel and reschedule links work and cannot be used to view a different booking

## Definition of done

The widget is live on the real website, a genuine client has booked through it unassisted, and the practitioner has approved that booking from their phone.
