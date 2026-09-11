# Monitoring and alerting

## Uptime (mandatory)

Probe **`https://<production-host>/health/ready`** from an off-box service (Better Stack EU or UptimeRobot), every 1 minute.

- `/health` — process is up (no I/O). Do not use this for uptime.
- `/health/ready` — PostgreSQL + object storage. Traefik and Dokploy should use this as the container health check.

Alert to the practitioner’s **SMS** (and optionally email). Acceptance: stop the app container and confirm an SMS within 5 minutes.

## What else to watch

| Signal | Where | Action |
|---|---|---|
| API 5xx spike | Traefik/access logs or Better Stack | Check Serilog `logs/equine-*.log`, recent deploy |
| Failed notifications | Per-failure email already sent; weekly digest counts them | [bounce-storm.md](bounce-storm.md), provider status |
| Hangfire stalled | `/hangfire` — `dispatch-due` should succeed every minute | [stuck-job-queue.md](stuck-job-queue.md) |
| Disk > 80% | VPS / Dokploy host metrics | Expand volume, prune unused images |
| DB connections | Postgres `pg_stat_activity` | Restart app (leaked scopes) or raise `max_connections` carefully |
| Backup older than 26h | Dokploy backup UI; weekly digest reminder | [backup.md](backup.md) |

## Weekly digest

Sunday morning Hangfire job `weekly-digest` emails bookings for the last week, unsigned journal count, failed notification count, and a backup reminder.

## Error tracking

Prefer **GlitchTip** on the same VPS (Sentry-compatible, no extra processor) unless a hosted EU Sentry is already chosen. File the DPA if a third party is used.

## Contacts

| Role | Channel |
|---|---|
| Practitioner | SMS + practice email |
| Operator / developer | Same as practitioner until delegated |
