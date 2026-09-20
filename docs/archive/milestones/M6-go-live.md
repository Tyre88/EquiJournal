# M6 — Go Live

**Goal:** move from "in use" to "the system of record", with historic data imported, backups proven, compliance documented, and an exit path that works.
**Estimated effort:** 1 week
**Prerequisites:** M0–M5 complete. Read `00-CONTEXT.md`.

> The single most important task in this milestone is the restore drill. An untested backup is not a backup. Do it first, not last.

---

## Scope

### In
Data import, backup and restore verification, production hardening, compliance documentation, monitoring, handover documentation, cutover.

### Out
New features of any kind. If something is missing, it goes on the post-launch list.

---

## Tasks

### 1. Backup and restore — do this first

- Nightly `pg_dump` to off-site storage, retained 30 daily / 12 monthly.
- WAL archiving for point-in-time recovery.
- Object storage (attachments) backed up or versioned independently.
- Backups encrypted at rest; the encryption key stored somewhere other than the server being backed up.

**Restore drill (mandatory, documented, repeatable):**
1. Provision a clean environment.
2. Restore the most recent backup, database and attachments.
3. Verify a known journal entry renders identically, attachments and all.
4. Record the wall-clock time it took.
5. Write it up in `/docs/runbooks/restore.md`.

Schedule this drill quarterly and put it in a calendar. A restore that has never been performed is a hypothesis.

### 2. Data import

- Build an importer for the existing records — spreadsheet, paper transcription, or an export from a previous system. Confirm the source with the practitioner first.
- Import clients and horses first, then historic treatments as **signed journal entries** marked with `source = Import` and the original date. Do not fabricate fields that did not exist in the source; leave them empty rather than inventing clinical content.
- Dry-run mode that reports what would be created and flags duplicates before writing anything.
- Duplicate detection on owner email/phone and horse name + owner.
- A post-import reconciliation report: counts in, counts created, rows skipped and why.
- Run the import into staging first and have the practitioner spot-check 20 random records against the source.

### 3. Production hardening

- HTTPS only, HSTS with a sensible max-age, TLS 1.2+ only.
- Secure, `HttpOnly`, `SameSite` cookies.
- Strict CSP on the admin app; `frame-ancestors` allowlist on the widget.
- Security headers: `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`.
- Database user the application connects as has no `DROP`, no `DELETE` on `journal_entries`, `journal_amendments` or `audit_log`. Enforce at the PostgreSQL grant level, not just in code.
- Secrets in a secret store or environment variables injected at deploy — never in the image, never in the repo.
- Automated dependency scanning in CI; a documented patching cadence.
- Rate limits reviewed against real traffic from M3–M5.
- Rotate every credential created during development before go-live.

### 4. Monitoring and alerting

- Uptime check on `/health/ready` from an external service, alerting to SMS.
- Alerts on: API error rate spike, failed notification threshold, background job stalled, database connection saturation, disk above 80%, backup job failure.
- A weekly automated email to the practitioner: bookings this week, unsigned journals, failed notifications, backup status.

### 5. Compliance documentation

Write these into `/docs/compliance/`:

- **Retention policy** — journals and attachments ≥ 5 years (recommend 10); booking metadata; notification log 12 months; audit log; account data. State the legal basis for each.
- **Register of processing activities** (*registerförteckning*) — GDPR Art. 30.
- **Privacy policy** for the public website, covering the widget.
- **Data processing agreements** (*personuppgiftsbiträdesavtal*) signed with: hosting provider, email provider, SMS provider, error tracking, object storage. File the signed copies.
- **Journal field mapping** — a table mapping each SJVFS 2023:19 required field to the system field that holds it. This is what you show an inspector. Have a colleague in the field review it.
- **Data breach response plan** — who does what, and the 72-hour notification obligation.
- **Erasure procedure** — exactly which data is deleted on a client's erasure request and which is retained under legal obligation, with the reasoning.

### 6. Exit path

Build and test both, then document them:

- **Full data export**: every owner, horse, booking, journal, amendment and attachment as a structured JSON + files ZIP.
- **Human-readable export**: all journals as PDFs, organised per horse.

If this system is ever replaced, the journals must be handed over intact. Prove it works now, while you still have the context to fix it.

### 7. Handover documentation

`/docs/`:
- Architecture overview with the diagram
- ADR index
- Local development setup
- Deployment and rollback procedure
- Runbooks: restore, key rotation, provider outage, bounce storm, stuck job queue
- Configuration reference — every setting, what it does, safe ranges
- A short **user manual in Swedish** covering the daily workflow and the admin panel

Assume the person reading this in two years has forgotten everything, and might not be you.

### 8. Cutover

1. Announce a short maintenance window.
2. Final import run against production.
3. Reconciliation check.
4. Enable notifications in production.
5. Publish the widget on the live website.
6. Retire the previous system: mark it read-only, archive an export of it, and keep that archive for the full retention period.
7. Monitor closely for two weeks with a daily check-in.

### 9. Post-launch backlog

Capture, do not build:
- Invoicing (Fortnox or Visma API is the pragmatic route in Sweden) and Swish/Stripe payments
- Deposits and cancellation fees for repeat no-shows
- Route optimisation with real drive times
- Photo/video comparison over time (before/after movement clips)
- Expanded client portal: exercise plans, home-care instructions
- Multi-practitioner scheduling and delegation records
- Group visits, if not already resolved in M2

---

## Acceptance criteria

- [ ] A full restore from backup has been performed into a clean environment and verified against a known record, with the elapsed time documented
- [ ] Historic data is imported, reconciled, and 20 random records spot-checked against the source by the practitioner
- [ ] The application database user cannot delete from `journal_entries`, `journal_amendments` or `audit_log` — verified by attempting it
- [ ] All development credentials rotated
- [ ] Uptime monitoring alerts reach a phone within 5 minutes of an induced outage
- [ ] Every DPA is signed and filed
- [ ] The journal field mapping covers every SJVFS 2023:19 required field and has been reviewed by someone in the field
- [ ] Full data export produces a complete, re-importable archive
- [ ] All journals export as readable per-horse PDFs
- [ ] A new person can set up a local environment from the README alone in under 30 minutes
- [ ] The widget is live on the production website and a real client has booked through it
- [ ] The previous system is archived read-only

## Definition of done

Two weeks of production operation with no data loss, no missed appointment caused by the system, and no manual workaround in daily use.
