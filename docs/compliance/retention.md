# Retention policy

Operating document for HästJournal. Not legal advice. Review with counsel if the practice grows beyond a single practitioner.

Recommended default for journals and attachments is **10 years**. The legal floor under SJVFS 2023:19 is **5 years**.

| Data | Retention | Legal basis | Purge |
|---|---|---|---|
| Journal entries, amendments, attachments, owner snapshots on the journal | ≥ 5 years, recommend 10 from `PerformedAt` | Legal obligation — lagen (2009:302), SJVFS 2023:19 | Do not purge automatically. Manual review after the period. |
| Booking metadata (visits, booking lines, locations used for a visit) | While a related journal is retained; then erasable | Contract / legitimate interest (scheduling). If a signed journal exists, keep the link. | Soft-delete or hard-delete only after journal retention lapses **and** no journal points at the line. |
| Notification log | 12 months | Legitimate interest — proof of delivery | `PurgeLogs` Hangfire job, weekly |
| Scheduled / in-app notifications | Until sent + 12 months, or until cancelled | Legitimate interest | Follow notification log |
| Audit log | Align with journal retention (recommend 10 years) | Accountability, Art. 5(2) GDPR | No automatic purge |
| Account data (Identity user, practice settings) | Until erasure request or account closure | Contract | Erase except journal-linked snapshots |
| Marketing consent + broadcast recipients | Until withdrawn | Consent, Art. 6(1)(a) | Immediate on withdraw |
| Widget / public booking contact details | Become an Owner row; then as owners | Contract (booking) | Soft-delete on erasure if no retained journal |
| Magic-link / consumed token hashes | Days (implementation TTL) | Security | Existing consumed-token table |
| Backups | 30 daily + 12 monthly dumps | Same bases as the data they contain | Dokploy retention; encrypted off-site |

Journal content about the **animal** is retained under veterinary law. The **owner’s** personal data in the journal is retained under the same legal obligation and is **not** erased on a GDPR request for as long as the journal must be kept. See [erasure.md](erasure.md).
