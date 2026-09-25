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

if [ -z "${SYNC_PASSWORD:-}" ]; then
    say "SYNC_PASSWORD is empty; nothing to apply. Skipping."
    exit 0
fi

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

if psql -h "$sock" -d postgres -v ON_ERROR_STOP=1 \
        -v pw="$SYNC_PASSWORD" \
        -c "ALTER USER CURRENT_USER WITH PASSWORD :'pw';"
then
    say "POSTGRES_PASSWORD applied to role $PGUSER."
else
    say "ALTER USER failed (exit $?); the cluster password was left untouched."
    say "If the API now reports 28P01, reset it by hand -- docs/runbooks/postgres-auth-failure.md (Fix A)."
fi

exit 0
