#!/usr/bin/env bash
# Restore a Dokploy/pg_dump archive into a clean database.
# Usage: PGHOST=localhost PGPORT=5432 PGUSER=postgres PGPASSWORD=postgres PGDATABASE=equijournal \
#        ./scripts/backup/restore.sh /path/to/latest.sql.gz
set -euo pipefail

DUMP="${1:?path to .sql, .sql.gz, or .dump}"
: "${PGHOST:=localhost}"
: "${PGPORT:=5432}"
: "${PGUSER:=postgres}"
: "${PGDATABASE:=equijournal}"

CONN=( -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" )

echo "Restoring $DUMP into $PGDATABASE@$PGHOST:$PGPORT"
if [[ "$DUMP" == *.dump ]]; then
  pg_restore --no-owner --role="$PGUSER" "${CONN[@]}" "$DUMP"
elif [[ "$DUMP" == *.gz ]]; then
  gunzip -c "$DUMP" | psql "${CONN[@]}"
else
  psql "${CONN[@]}" -f "$DUMP"
fi

echo "Restore finished. Run verify-known-record.sql next."
