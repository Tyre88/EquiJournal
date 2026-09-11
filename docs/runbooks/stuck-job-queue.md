# Stuck Hangfire queue

Dashboard: `https://<host>/hangfire` (Admin JWT / Hangfire admin filter).

## Symptoms

- Reminders or daily summary not sending
- `dispatch-due` last success older than 10 minutes
- Jobs in Failed / retries exhausted

## Steps

1. Confirm the API container is running and `/health/ready` is OK (Hangfire uses the same Postgres).
2. Open Recurring jobs: `dispatch-due` (`*/1 * * * *`), `expire-unverified`, `daily-summary`, `weekly-digest`, `unsigned-journals`, `follow-ups`, `purge-logs`, `deliverability`.
3. Trigger `dispatch-due` once. Watch Succeeded / Failed.
4. Open Failed jobs. Read the exception. Typical causes: Postmark/46elks credentials, quiet hours, missing templates.
5. Requeue a single failed job after fixing the cause. Do not requeue a bounce storm ([bounce-storm.md](bounce-storm.md)).
6. If the server is stopped, check Dokploy logs for Hangfire PostgreSQL lock or connection errors. One Hangfire server per environment is enough.

## After recovery

Confirm a test reminder or the daily summary in the notification log. Note the incident in the weekly check-in during the first two production weeks.
