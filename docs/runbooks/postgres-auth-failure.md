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

## Most likely cause: the volume was initialized with a different password

`POSTGRES_PASSWORD` is only read by `initdb`, the **first** time the `postgres_data`
volume is created. Changing `POSTGRES_PASSWORD` in Dokploy and redeploying does not
change the password of an already-initialized cluster. The API then presents the new
password to a server that still has the old one.

This is the default explanation when the stack worked before and broke after a secret
rotation, an env-var edit, or a re-paste of `.env.dokploy.example`.

### Fix A — reset the password in the running cluster (keeps all data, preferred)

```bash
# on the Dokploy host
docker compose -p equilog exec postgres \
  psql -U postgres -d equijournal \
  -c "ALTER USER postgres WITH PASSWORD 'THE-VALUE-IN-DOKPLOY';"
```

`exec … psql -U postgres` authenticates over the local socket (trust/peer), so it works
even though TCP password auth is failing. Use the exact string from the Dokploy
`POSTGRES_PASSWORD` variable, then restart the `api` service.

If the shell rejects the quoting, write the statement to a file and use `psql -f`
rather than escaping by hand.

### Fix B — point the API back at the old password

If you still have the password the volume was initialized with, set `POSTGRES_PASSWORD`
back to it and redeploy. Then do Fix A at a calmer moment if you actually wanted to
rotate.

### Fix C — re-initialize the volume (destroys the database)

Only when the data is disposable (fresh environment, never seeded).

```bash
docker compose -p equilog down
docker volume rm equilog_postgres_data
docker compose -p equilog -f docker-compose.dokploy.yml up -d
```

Take a dump first if there is any doubt — see [backup.md](backup.md) and
[restore.md](restore.md).

## Second cause: the password never survived interpolation

`ConnectionStrings__Default` is assembled in
[docker-compose.dokploy.yml](../../docker-compose.dokploy.yml) as
`…;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}`. Two characters break it:

- **`$`** — Docker Compose interpolates it before the container sees it. `pa$$word`
  arrives as `paword`. Escape as `$$` in an env file, or avoid `$`.
- **`;`** — terminates the Npgsql keyword/value pair, silently truncating the password.

`openssl rand -base64 48` produces neither, which is why the generator in
[.env.dokploy.example](../../.env.dokploy.example) is the recommended source. If the
password came from somewhere else, check for those two characters first.

Confirm what the container actually received:

```bash
docker compose -p equilog exec api printenv ConnectionStrings__Default
```

Compare it character-for-character against the Postgres side:

```bash
docker compose -p equilog exec postgres printenv POSTGRES_PASSWORD
```

Note that the second command shows what the *current* container was started with, which
is not necessarily what the volume was initialized with — that is exactly the trap in
the first cause above.

## Verify the fix

```bash
# auth over TCP as the API does, from inside the API container
docker compose -p equilog exec api \
  curl -fsS http://127.0.0.1:8080/health/ready
```

Then confirm the container stays up across a restart and that one admin login works.

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
