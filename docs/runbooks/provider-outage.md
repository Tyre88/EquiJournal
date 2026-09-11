# Provider outage

Stay on the daily workflow. Do not invent a second booking channel that is not written back into the system.

## Email (Postmark)

1. Check https://status.postmarkapp.com.
2. Hangfire will retry scheduled mail. Do not flush the queue blindly.
3. Tell clients that confirmations may be delayed; bookings in admin are still the source of record.
4. If Postmark is down at cutover, keep notifications disabled until SPF/DKIM and the provider recover ([email-deliverability.md](email-deliverability.md)).

## SMS (46elks)

1. Check 46elks status / dashboard.
2. Reminders and verification SMS will fail; email and in-app still work.
3. Daily SMS cap alerts will not send — watch the notification log.

## Object storage

1. Journals remain writable; **new attachments** will fail.
2. Do not delete local copies of photos until uploads succeed.
3. Restore from `equine/attachments/` if the live bucket is lost ([restore.md](restore.md)).

## Google Maps / Nominatim / OSRM

Address search and travel time degrade. Type the address manually. Schema order can be arranged by hand for the day.

## Postgres / VPS

1. If the API is down, Traefik will fail `/health/ready` and the uptime SMS should fire.
2. Do not restore onto the live volume until you have a copy of the latest dump.
3. Rollback the Dokploy image if the outage started after a deploy ([deploy-and-rollback.md](../deploy-and-rollback.md)).
