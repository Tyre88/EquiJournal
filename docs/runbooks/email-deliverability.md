# E-postleverans

Sending domain must have SPF, DKIM and DMARC before production reminders are enabled.

## DNS

1. **SPF** — `v=spf1 include:spf.mtasv.net -all` (Postmark) or the record Postmark shows for the domain.
2. **DKIM** — CNAME records from the Postmark sender signature.
3. **DMARC** — `_dmarc` TXT `v=DMARC1; p=quarantine; rua=mailto:dmarc@YOURDOMAIN`.

CI runs a DNS check when `Notifications:SendingDomain` is set and fails if SPF or DMARC is missing. Localhost is skipped.

## Headers

Every HTML email has a plain-text alternative. Confirmation and marketing mail set `Reply-To` to the practice email and `List-Unsubscribe` when a link exists.

## Inbox test (manual)

Send a confirmation to Gmail, Outlook and a Swedish provider (telia.com / hotmail.se). Record that it landed in the inbox, not spam, in the go-live checklist.
