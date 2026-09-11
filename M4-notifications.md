# M4 — Notifications

**Goal:** the system reliably sends the right message to the right person at the right time, over email and SMS, with a full delivery log.
**Estimated effort:** 1.5 weeks
**Prerequisites:** M0–M3 complete. Read `00-CONTEXT.md`.

> Reminders reduce no-shows more than any other feature in this system. They must be reliable, and reliability here means: survive a restart, never double-send, and never silently fail.

---

## Scope

### In
Email and SMS provider integration, editable templates, a durable scheduled-notification queue, delivery logging, `.ics` generation, deliverability setup.

### Out
Marketing campaigns. In-app push. Anything that isn't transactional.

---

## Tasks

### 1. Providers

**Email:** Postmark or Brevo. Both are EU-usable and good at transactional delivery. Write an ADR for the choice.
**SMS:** 46elks (Swedish, simple API, cheap for domestic traffic) or Twilio.

Abstract both behind `IEmailSender` / `ISmsSender` with a fake implementation for tests and MailHog/Mailpit for local development. **Never** send real messages from a non-production environment — guard this at the composition root, not with a config flag someone can forget.

### 2. Deliverability — do this first, not last

- SPF, DKIM and DMARC records on the sending domain, documented in `/docs/runbooks/email-deliverability.md`.
- Correct `From`, `Reply-To`, `List-Unsubscribe` headers.
- Plain-text alternative for every HTML email.
- Test against Gmail, Outlook and a Swedish provider (telia.com / hotmail.se are common among clients) before going live.

Without this, reminders go to spam and the entire milestone is worthless.

### 3. Durable scheduled queue

Table `scheduled_notifications`: id, type, channel, recipient, related entity type + id, `scheduled_for`, payload (JSONB), status (`Pending` \| `Sent` \| `Failed` \| `Cancelled`), attempts, last_error, `sent_at`, provider_message_id.

A hosted service ticks on a configurable interval, claims due rows with `SELECT ... FOR UPDATE SKIP LOCKED`, and dispatches them.

**Requirements:**
- The queue lives in PostgreSQL. Nothing is lost on restart or deploy.
- Idempotent: a unique key per (type, entity id, channel) prevents duplicate sends.
- Exponential backoff on retry, capped at 5 attempts, then `Failed` with an alert to the practitioner.
- Cancelling or rescheduling a booking **cancels** its pending notifications and schedules new ones.

Start with `IHostedService`. Introduce Hangfire or Quartz only if the job needs genuinely outgrow this — do not reach for it now.

### 4. Notification catalogue

| Trigger | Channel | Recipient |
|---|---|---|
| Booking requested via widget | Email (verification) | Client |
| Booking requested via widget | Email + optional SMS | Practitioner |
| Booking confirmed / approved | Email with `.ics` + SMS | Client |
| Booking rescheduled | Email with updated `.ics` + SMS | Client |
| Booking cancelled by either party | Email | Both |
| 24 h before appointment | SMS, email fallback if no phone | Client |
| Same-day summary of the day's schedule | Email at a configurable morning time | Practitioner |
| Follow-up due (from treatment `follow_up_interval`) | Email | Client |
| Journal unsigned for more than 24 h | Email | Practitioner |
| Notification permanently failed | Email | Practitioner |

Each type is individually toggleable in settings, and reminder lead time is configurable.

### 5. Templates

- Editable in the admin panel with a fixed, documented set of merge fields per notification type (`{{klientnamn}}`, `{{hästnamn}}`, `{{behandling}}`, `{{datum}}`, `{{tid}}`, `{{adress}}`, `{{avbokningslänk}}`).
- Validate on save: unknown merge fields are rejected.
- Live preview with sample data.
- Separate subject and body; separate SMS body with a character counter and segment count (SMS costs money per segment — show it).
- Swedish by default. Keep templates in the database, not in code.

### 6. `.ics` generation

Attach a valid iCalendar file to confirmation and reschedule emails: correct `DTSTART`/`DTEND` in `Europe/Stockholm` with a VTIMEZONE block, `UID` stable across updates, `SEQUENCE` incremented on reschedule, `METHOD:REQUEST`, location as the stable address, description with the treatment and practitioner contact details. Test it imports cleanly into Google Calendar, Apple Calendar and Outlook.

### 7. Delivery log

Table `notification_log`: every attempt with channel, recipient, template, rendered subject, provider message id, status, provider response, timestamp.

- Surfaced on the booking detail screen: "Bekräftelse skickad 12 mars 08:14, levererad".
- Webhook endpoints for provider delivery/bounce callbacks, updating the log.
- Hard bounces flag the owner's email as invalid and alert the practitioner.
- Retention: 12 months, then purge. Document this in the retention policy.

### 8. Quiet hours and rate sanity

- No SMS sent outside configurable hours (default 07:00–21:00 local) — reminders shift to the nearest allowed time.
- A daily cap on outbound SMS as a runaway-loop safety net; exceeding it alerts instead of sending.

---

## Acceptance criteria

- [ ] A confirmed booking produces a confirmation email with a valid `.ics` that imports into Google, Apple and Outlook calendars
- [ ] A 24-hour reminder SMS is scheduled on confirmation and sent at the right local time, including across a DST boundary
- [ ] Rescheduling a booking cancels the old reminder and schedules a new one — no duplicate is sent
- [ ] Restarting the API mid-queue loses nothing; pending notifications still fire
- [ ] A forced provider failure retries with backoff and eventually alerts the practitioner
- [ ] The same notification cannot be sent twice, verified by an integration test that runs the dispatcher twice concurrently
- [ ] No message is sent from the staging environment under any configuration
- [ ] SPF, DKIM and DMARC pass; a test email lands in the inbox, not spam, on three major providers
- [ ] A template with an unknown merge field is rejected on save
- [ ] SMS scheduled for 03:00 is deferred to the start of quiet-hours-end
- [ ] Delivery status is visible on the booking detail screen

## Definition of done

A full week of real bookings runs with reminders enabled, and no client reports a missing, duplicated or mistimed message.

## Settings hub (this implementation)

Admin `/settings`: Konto, Verksamhet, Aviseringar, Widget (configure + embed how-to). Also `/uppfoljningar`, `/utskick`, and client portal at `/portal/`.
