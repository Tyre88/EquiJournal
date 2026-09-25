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

`depends_on: condition: service_healthy` waits until a TCP login with
`POSTGRES_PASSWORD` succeeds. `pg_isready` alone is not enough — it only checks
that the server answers — so the healthcheck also runs `psql -h 127.0.0.1`.
The API additionally retries `28P01` for about 60 seconds inside
`EnsureDatabaseExists` while that login is still failing. A password that is
still wrong after both windows takes the process down.

Dokploy generates its own Compose project name (e.g. `equijournal-fullstack-451i7h`),
so run `docker compose ls` first and substitute it for `-p equilog` in the commands
below.

## Step 1 — find out which of the two causes it is

Both produce an identical `28P01`. You cannot tell them apart from the API log, and the
fix differs, so check before changing anything. Dokploy gives every service a **Terminal**
tab — you do not need SSH or `docker compose` for this.

In the **api** service terminal:

```bash
printenv Postgres__Password
```

In the **postgres** service terminal:

```bash
printenv POSTGRES_PASSWORD
```

Compare the two values character for character. Watch the end of the string especially.

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

The API no longer receives a pre-built connection string. Compose passes
`Postgres__Password` (and host, port, database, user) and
[Program.cs](../../src/Equine.Api/Program.cs) builds `ConnectionStrings:Default`
with `NpgsqlConnectionStringBuilder`, which quotes the password. A `;` in the
password used to terminate the keyword/value pair and produce this same `28P01`
while Postgres held the full value. One character still breaks the env var
itself:

- **`$`** — Docker Compose interpolates it before either container starts. `pa$$word`
  arrives as `paword`. This one hits both services equally, so they stay consistent
  with each other but neither matches what you typed into Dokploy.

Leading or trailing whitespace will also bite you, and is invisible in the Dokploy UI.

Fix: set `POSTGRES_PASSWORD` to a value with no `$` and no leading or trailing space. `openssl rand -base64
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

`EnsureDatabaseExists` runs before `app.Run()`. It retries only `28P01` for about
60 seconds, long enough for the in-container password sync to finish. Any other
connection error, and a password that is still wrong after that window, takes the
process down instead of leaving a degraded-but-running API. That is the intended
behaviour — a misconfigured database is not something to serve traffic through —
but it means the only signal after the retry window is the crash loop in the
Dokploy logs.

## Automatic reconciliation (in the `postgres` service)

`docker-compose.dokploy.yml` wraps the `postgres` container's command so that, a few
seconds after the server accepts connections, it runs

```sql
ALTER USER CURRENT_USER WITH PASSWORD :'pw';
```

against the **local unix socket**, where the image's `pg_hba.conf` grants `trust`. That
works even while TCP password auth is failing, which is the whole point. It retries
`ALTER USER` for about a minute (12 attempts, 5 seconds apart) instead of giving up
after a few tries. The postgres healthcheck does not pass until a TCP login with
`POSTGRES_PASSWORD` succeeds, so `api` waits on `service_healthy` rather than
connecting early. The effect is that `POSTGRES_PASSWORD` becomes authoritative on
every deploy instead of only at `initdb`, so Step 2A below should not normally be
needed any more.

It is deliberately built so it cannot make things worse:

- It runs in the background and the stock entrypoint is `exec`'d regardless, so it can
  never fail, block or slow a deploy. Worst case it logs and the password is unchanged.
- It lives inside the `postgres` container, so there is no shared socket volume and no
  extra service for `api` to depend on.

Check what it did:

```
docker compose -p <project> logs postgres | grep password-sync
```

- `POSTGRES_PASSWORD applied to role postgres.` — reconciled.
- `ALTER USER failed; cluster password left unchanged.` — fall back to Step 2A.
- `postgres never became ready; skipped.` — the cluster itself is the problem, not auth.

### Two ways this was got wrong before

Both were shipped and both broke a deploy (PRs #60-#63, 2026-09-25). Do not reintroduce
either:

1. A separate `postgres-password-sync` service gated `api` on
   `service_completed_successfully`, so a non-zero exit took the whole deploy down —
   turning a recoverable stale password into a hard outage.
2. The statement was passed as `psql -c "… PASSWORD :'pw'"`. psql only interpolates
   variables in input it parses itself; `-c` hands the string straight to the server,
   which rejected the literal `:'pw'` with `syntax error at or near ":"`. It must go in
   on **stdin**. Keep the `:'pw'` form rather than splicing the password into the SQL —
   that is what keeps a password containing a single quote safe.

## Related

- `ASPNETCORE_ENVIRONMENT` is intentionally `Development` in the Dokploy stack: the
  schema is created by `EnsureCreatedAsync` inside the non-Production branch of
  `EnsureDatabaseExists`, and there are no EF migrations in the repo. Do not "harden"
  it to `Production` without first adding a migration path — the app would start
  against an empty database. See [deploy-and-rollback.md](../deploy-and-rollback.md).
- Credential rotation: [rotate-provider-keys.md](rotate-provider-keys.md).
