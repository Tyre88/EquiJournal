#!/bin/sh
# Makes POSTGRES_PASSWORD authoritative on every deploy, not just at initdb.
#
# POSTGRES_PASSWORD is read by initdb only the first time the data volume is
# created. Rotating it afterwards leaves the cluster on the old password and the
# API crash-loops with SqlState 28P01. This reconciles the two by connecting over
# the Unix socket -- which the image's pg_hba.conf trusts -- so it works even while
# TCP password auth is failing.
#
# This script ALWAYS exits 0. A failure here must never block a deploy: the worst
# case is that the password stays as it was, which is exactly where we would be
# without this step. Read the log line instead.
#
# See docs/runbooks/postgres-auth-failure.md.

say() { echo "password-sync: $*"; }

: "${PGUSER:=postgres}"
: "${SYNC_TCP_HOST:=postgres}"
: "${SYNC_DB:=postgres}"

if [ -z "${SYNC_PASSWORD:-}" ]; then
    say "SYNC_PASSWORD is empty; nothing to apply. Skipping."
    exit 0
fi

# Exactly what the API does: authenticate over TCP with the configured password.
tcp_auth_works() {
    PGPASSWORD="$SYNC_PASSWORD" psql -h "$SYNC_TCP_HOST" -U "$PGUSER" -d "$SYNC_DB" \
        -tAc 'SELECT 1' >/dev/null 2>&1
}

if tcp_auth_works; then
    say "the configured password already authenticates over TCP; nothing to do."
    exit 0
fi

say "configured password does not authenticate over TCP; reconciling the cluster."

# The socket directory differs between images and between /var/run and /run on
# Alpine, so probe rather than assume. Postgres is already healthy by the time we
# run, but the shared volume can take a moment to surface the socket.
sock=""
i=0
while [ "$i" -lt 30 ]; do
    for dir in /var/run/postgresql /run/postgresql /tmp; do
        if [ -S "$dir/.s.PGSQL.5432" ]; then
            sock="$dir"
            break
        fi
    done
    [ -n "$sock" ] && break
    i=$((i + 1))
    sleep 1
done

if [ -z "$sock" ]; then
    say "no Postgres socket found in /var/run/postgresql, /run/postgresql or /tmp after 30s."
    say "The shared socket volume is not working; the cluster password was left untouched."
    say "Reset it by hand -- see docs/runbooks/postgres-auth-failure.md (Fix A)."
    exit 0
fi

say "using socket $sock, role $PGUSER"

# The statement goes in on stdin, NOT via -c. psql only performs variable
# interpolation on input it parses itself; -c hands the string straight to the
# server, which then chokes on the literal :'pw'. Getting this wrong is what broke
# the 2026-09-25 deploy. :'pw' (rather than "$SYNC_PASSWORD" spliced into the SQL)
# lets psql do the literal quoting, so a password containing a single quote is safe.
if printf "%s\n" "ALTER USER CURRENT_USER WITH PASSWORD :'pw';" |
    psql -h "$sock" -d "$SYNC_DB" -q -v ON_ERROR_STOP=1 -v pw="$SYNC_PASSWORD"
then
    if tcp_auth_works; then
        say "POSTGRES_PASSWORD applied to role $PGUSER and verified over TCP."
    else
        say "ALTER USER succeeded but TCP auth still fails."
        say "Check pg_hba.conf, or a ';' in POSTGRES_PASSWORD truncating the API's"
        say "connection string -- docs/runbooks/postgres-auth-failure.md."
    fi
else
    say "ALTER USER failed; the cluster password was left untouched."
    say "If the API now reports 28P01, reset it by hand -- docs/runbooks/postgres-auth-failure.md (Fix A)."
fi

exit 0
