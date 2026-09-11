# Backups (Dokploy + S3)

Configure this on the VPS. The application does not take its own dumps.

## Database (Dokploy)

1. In Dokploy → Settings → Destinations, add an **EU** S3-compatible bucket (Hetzner Object Storage, Backblaze B2 EU, or Cloudflare R2 EU). Enable server-side encryption. Store access keys in Dokploy secrets and a password manager — never on the Postgres data volume.
2. On the Postgres service → Backup:
   - Destination: the EU bucket
   - Prefix: `equine/db/`
   - Schedule: `0 2 * * *` (nightly 02:00)
   - Keep: **30**
   - Enable and click **Test**
3. Add a second job: schedule `0 3 1 * *`, prefix `equine/db/monthly/`, keep **12**.

Dokploy uses `pg_dump` (often gzip). That is a full dump, not PITR.

## Attachments

Pick one:

- Enable object-storage **versioning** on the attachments bucket, or
- Nightly `rclone sync` from the live bucket to `s3:bucket/equine/attachments/` (`0 3 * * *` on the host).

## WAL / point-in-time recovery

Dokploy dumps are not continuous. If the Postgres compose service allows custom config, mount [scripts/backup/postgresql-wal.conf](../../scripts/backup/postgresql-wal.conf) and an `archive_command` that rclone-copies WAL to `equine/wal/`.

If the managed Dokploy Postgres image cannot take `postgresql.conf` overrides, **RPO is the last nightly dump**. Record that gap here and keep WAL on the post-launch list.

WAL status for this deployment: **not enabled** until the operator confirms compose overrides work.

## Encryption

- Bucket SSE on.
- Encryption / IAM keys stored off the database host.
- Rotate keys per [rotate-provider-keys.md](rotate-provider-keys.md).

## Failure signal

- Turn on Dokploy backup-failure notifications (email).
- The Sunday weekly digest reminds the practitioner to confirm the last dump is newer than 26 hours.
- After any failed job: fix destination credentials, run a manual backup, then a restore drill on staging.

## Related

- [restore.md](restore.md) — mandatory drill
- [scripts/backup/](../../scripts/backup/) — restore and grant-check helpers
