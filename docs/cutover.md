# Cutover checklist

Operator steps. Code does not flip production by itself.

1. Announce a short maintenance window.
2. Final CSV import: dry-run on production connection, then `--commit`. Save `import-report.json`.
3. Practitioner spot-checks **20** random journals against the source.
4. Rotate any credential that ever lived in git ([rotate-provider-keys.md](runbooks/rotate-provider-keys.md)). The old committed database password is compromised.
5. Enable real Postmark/46elks. Confirm SPF/DKIM/DMARC ([email-deliverability.md](runbooks/email-deliverability.md)).
6. Publish the widget snippet on the live website. One real client books.
7. Previous system: read-only, export archived for the full journal retention period.
8. Daily check-in for two weeks: uptime SMS, failed notifications, unsigned journals, backup age.

Definition of done (from M6): two weeks of production with no data loss, no missed appointment caused by the system, and no daily workaround.
