# Stuck Hangfire queue

The API runs a Hangfire server and registers recurring jobs at startup. There is no dashboard.

## Symptoms

- Reminders or daily summary not sending
- `dispatch-due` last success older than 10 minutes
- Jobs in Failed / retries exhausted

## Steps

1. Confirm the API container is running and `/health/ready` is OK (Hangfire uses the same Postgres).
2. Recurring jobs: `dispatch-due` (`*/1 * * * *`), `expire-unverified` (`*/1 * * * *`), `follow-ups` (`0 7 * * *`), `unsigned-journals` (`0 * * * *`), `daily-summary` (`*/15 * * * *`), `weekly-digest` (`0 7 * * 0`), `purge-logs` (`0 3 * * 0`), `deliverability` (`0 8 * * *`).
3. Read API logs for exceptions from those jobs. Typical causes: Postmark/46elks credentials, quiet hours, missing templates.
4. Do not requeue a bounce storm ([bounce-storm.md](bounce-storm.md)).
5. If the server is stopped, check Dokploy logs for Hangfire PostgreSQL lock or connection errors. One Hangfire server per environment is enough.

## After recovery

Confirm a test reminder or the daily summary in the notification log. Note the incident in the weekly check-in during the first two production weeks.
