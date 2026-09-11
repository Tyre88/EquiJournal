# Restore from backup

An untested backup is not a backup. Run this drill after the first production dump lands, then **every quarter**.

Dokploy takes nightly `pg_dump` archives to an EU S3-compatible bucket (`equine/db/`). Attachments are versioned in object storage or copied nightly to `equine/attachments/`. WAL archiving (point-in-time recovery) is optional — see [backup.md](backup.md). If WAL is not enabled, RPO is the last successful nightly dump.

## Prerequisites

- Off-site S3 credentials (Dokploy destination). The bucket encryption key lives in a password manager, **not** on the Postgres volume.
- A clean target: local `docker compose` (throwaway) or a Dokploy staging stack. Never restore onto the live volume.
- Known production journal id to verify (record it below after the first drill).

## Local drill (recommended first run)

1. Start a clean Postgres + MinIO (use a separate compose project so you do not touch local data):

   ```bash
   docker compose -p equine-restore -f docker-compose.yml up -d postgres minio minio-init
   ```

2. Download the latest dump and attachment prefix from S3 (rclone, AWS CLI, or Dokploy download).

3. Restore the database:

   ```bash
   # custom-format / gzip dump from Dokploy
   gunzip -c latest.sql.gz | psql "Host=localhost;Port=5432;Database=equijournal;Username=postgres;Password=postgres"
   # or:
   pg_restore --no-owner --role=postgres -d equijournal latest.dump
   ```

   Helpers: [scripts/backup/restore.sh](../../scripts/backup/restore.sh).

4. Restore attachments into the MinIO bucket `equine-attachments` (rclone or `mc mirror`).

5. Start the API against that connection string and MinIO (`Storage__*` env vars). Do **not** enable Postmark/46elks.

6. Open the known journal in admin. Confirm body text, amendments, attachment names and that downloads open.

7. Run [scripts/backup/verify-known-record.sql](../../scripts/backup/verify-known-record.sql) with the known journal id.

8. Record wall-clock time below. Tear down: `docker compose -p equine-restore down -v`.

## Dokploy staging drill

1. Create a throwaway Postgres + empty bucket in Dokploy.
2. Restore the latest dump (Dokploy “restore” or `pg_restore` into the new service).
3. Rclone `equine/attachments/` into the new bucket.
4. Point a staging app instance at the restored DSN and bucket. Repeat verification steps 6–8.

## Quarterly drill log

| Date | Environment | Dump used | Known journal id | Elapsed | Verifier | Notes |
|---|---|---|---|---|---|---|
| _pending first drill_ | | | | | | Schedule this after the first nightly dump. |

Next scheduled drill: **three months after the last completed row**. Put it on a calendar.

## If restore fails

1. Do not retry onto production.
2. Keep the failed target for logs.
3. Try the previous daily dump, then the latest monthly.
4. If WAL is enabled, restore the base backup and replay WAL to the desired timestamp (see [backup.md](backup.md)).
