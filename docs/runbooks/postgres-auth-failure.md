# Postgres auth failure on startup (SqlState 28P01)

## Symptom

The API container exits during startup. The log ends with a Npgsql stack trace through
`EnsureDatabaseExists` ([Program.cs](../../src/Equine.Api/Program.cs)) and:

```
Severity: FATAL
SqlState: 28P01
MessageText: password authentication failed for user "postgres"
Routine: auth_failed
```

`/health/ready` never comes up, so Traefik has no healthy backend and the domain returns 502.

## What 28P01 actually means

The server was reached and it rejected the credentials. This is **not** a networking,
DNS or "Postgres isn't up yet" problem — those produce connection-refused or timeout,
not `auth_failed`. So `Host=` is right and the password is wrong.

`depends_on: condition: service_healthy` does not protect you here: the compose
healthcheck is `pg_isready -U postgres`, which checks that the server answers, not that
the password matches.

Dokploy generates its own Compose project name (e.g. `equijournal-fullstack-451i7h`),
so run `docker compose ls` first and substitute it for `-p equilog` in the commands
below.

## Step 1 — find out which of the two causes it is

Both produce an identical `28P01`. You cannot tell them apart from the API log, and the
fix differs, so check before changing anything. Dokploy gives every service a **Terminal**
tab — you do not need SSH or `docker compose` for this.

In the **api** service terminal:

```bash
printenv ConnectionStrings__Default
```

In the **postgres** service terminal:

```bash
printenv POSTGRES_PASSWORD
```

Compare the `Password=` section of the first against the second, character for
character. Watch the end of the string especially.

- **They differ** → cause B. The password is being mangled on its way into the
  connection string only. Go to Step 2B.
- **They match** → cause A. Both containers agree; it is the stored cluster password
  that is stale. Go to Step 2A.

## Step 2A — the cluster holds an older password

`POSTGRES_PASSWORD` is only read by `initdb`, the **first** time the `postgres_data`
volume is created. Changing it in Dokploy and redeploying does not change the password
of an already-initialized cluster. The API then presents the new password to a server
that still has the old one.

This is the default explanation when the stack worked before and broke after a secret
rotation — including [cutover.md](../cutover.md) step 4, "rotate any credential that
ever lived in git".

Fix it in the **postgres** service terminal, which authenticates over the local socket
(trust auth) and so works even though TCP password auth is failing:

```bash
psql -U postgres -d equijournal \
  -c "ALTER USER postgres WITH PASSWORD 'PASTE-POSTGRES_PASSWORD-EXACTLY';"
```

Paste the literal value from Step 1, not a shell variable. If it contains a single
quote, double it (`it's` → `it''s`). Then restart the `api` service.

Note this is a plain SQL string. Do **not** reach for `psql -v` and `:'var'` here: psql
only interpolates variables in input it parses itself, and `-c` hands the string
straight to the server. See the note at the end of this file.

## Step 2B — the password is mangled before Postgres ever sees it

The API's connection string is assembled in
[docker-compose.dokploy.yml](../../docker-compose.dokploy.yml) as
`…;Username=…;Password=${POSTGRES_PASSWORD}`. Two characters break that:

- **`;`** — terminates the Npgsql keyword/value pair, silently truncating the password.
  Postgres itself still receives the full value, so the two sides disagree.
- **`$`** — Docker Compose interpolates it before either container starts. `pa$$word`
  arrives as `paword`. This one hits both services equally, so they stay consistent
  with each other but neither matches what you typed into Dokploy.

Leading or trailing whitespace will also bite you, and is invisible in the Dokploy UI.

Fix: set `POSTGRES_PASSWORD` to a value containing none of those. `openssl rand -base64
48` is safe — it emits only `A-Za-z0-9+/=`, all of which pass through intact. Then run
**Step 2A as well**, because changing the variable still will not change the stored
cluster password, and redeploy.

## If the database has nothing worth keeping

Fastest certain fix, and reasonable before cutover. `initdb` runs again and takes the
current `POSTGRES_PASSWORD`, so both causes disappear at once.

**This destroys the database.** Check [cutover.md](../cutover.md) first — if the CSV
import in step 2 has already run, that data is in this volume.

```bash
docker compose -p <project> down
docker volume rm <project>_postgres_data
docker compose -p <project> -f docker-compose.dokploy.yml up -d
```

Take a dump first if there is any doubt — [backup.md](backup.md), [restore.md](restore.md).

## Verify

```bash
curl -fsS http://127.0.0.1:8080/health/ready
```

from the **api** service terminal. Then confirm the container survives a restart and
that one admin login works.

## Why the failure is fatal rather than retried

`EnsureDatabaseExists` runs before `app.Run()` and has no retry or backoff around the
initial connection, so a bad credential takes the process down instead of leaving a
degraded-but-running API. That is the intended behaviour — a misconfigured database is
not something to serve traffic through — but it means the only signal is the crash loop
in the Dokploy logs.

## Previously attempted: automatic reconciliation

A `postgres-password-sync` compose service that ran `ALTER USER` over the local socket
on every deploy was added and then reverted (PRs #60-#63, 2026-09-25). It failed twice
in production:

1. It gated `api` on `service_completed_successfully`, so when the sync exited 1 it took
   the whole deploy down — turning a recoverable stale password into a hard outage.
2. The statement was passed as `psql -c "… PASSWORD :'pw'"`. psql only interpolates
   variables in input it parses itself; `-c` hands the string straight to the server,
   which rejected the literal `:'pw'` with `syntax error at or near ":"`.

Both are fixable, and the third iteration was never deployed. If you revisit this: the
step must always exit 0, the statement must go in on stdin, and the result must be
verified with a real TCP login rather than inferred from the exit code. But weigh that
against how rarely the password actually rotates — the manual `ALTER USER` above is one
command and has no failure mode of its own.

## Related

- `ASPNETCORE_ENVIRONMENT` is intentionally `Development` in the Dokploy stack: the
  schema is created by `EnsureCreatedAsync` inside the non-Production branch of
  `EnsureDatabaseExists`, and there are no EF migrations in the repo. Do not "harden"
  it to `Production` without first adding a migration path — the app would start
  against an empty database. See [deploy-and-rollback.md](../deploy-and-rollback.md).
- Credential rotation: [rotate-provider-keys.md](rotate-provider-keys.md).
