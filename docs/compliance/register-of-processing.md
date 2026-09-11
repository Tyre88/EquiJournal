# Register of processing activities (registerförteckning)

GDPR Article 30. Controller: the practitioner / practice named in practice settings (Skåne, Sweden).

| # | Purpose | Categories of data subjects | Personal data | Recipients | Transfer outside EU | Retention | Security |
|---|---|---|---|---|---|---|---|
| 1 | Veterinary journals | Animal owners | Name, address, phone, email; animal identity in the same record | Hosting VPS, encrypted backups, PDF export to the practitioner | No, if host and S3 are EU | [retention.md](retention.md) | Access control, TLS, 2FA, DB grants, append-only journals |
| 2 | Booking and travel | Animal owners | Contact, stable address, geo if entered | Same + optional Maps/Nominatim lookups | Nominatim/OSRM if used (public APIs) | With booking / journal | Same |
| 3 | Public widget booking | Prospective clients | Name, email, phone, horse name, address | Email (Postmark), SMS (46elks) | Postmark/46elks per their DPA (EU/EEA or documented) | Becomes owner/booking | Rate limits, no owner names in public slot API |
| 4 | Client portal magic link | Owners with portal access | Email, booking/journal summary if shared | Postmark | As email provider | Session + token TTL | One-time link, security stamp |
| 5 | Notifications | Owners, practitioner | Email, phone, message content | Postmark, 46elks, in-app store | As providers | 12 months log | Quiet hours, SMS cap |
| 6 | Marketing broadcasts | Consenting owners | Email, name | Postmark | As email provider | Until withdraw | Consent flag |
| 7 | Accounts / audit | Practitioner, assistants | Email, display name, login events | Host | No | Account life / journal-aligned audit | JWT, TOTP, Hangfire admin filter |
| 8 | Uptime / errors | None intended (IP of probes) | IP, URL, error stacks if GlitchTip/Sentry | Uptime vendor and/or self-hosted GlitchTip | Avoid US-only vendors | Vendor default / self-host | No journal bodies in traces |

No sale of personal data. No automated decision-making with legal effect.
