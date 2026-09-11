# Data processing agreements (personuppgiftsbiträdesavtal)

The practitioner is controller. File **signed** copies (PDF) in the practice’s records folder — not only links. This repo holds the checklist, not the signatures.

| Processor | Purpose | DPA | Status | Filed |
|---|---|---|---|---|
| VPS / hosting provider | Compute, disk | Vendor Art. 28 DPA | _unsigned_ | |
| Traefik/Let’s Encrypt on same VPS | TLS | Covered by hosting if self-operated | n/a if self-hosted | |
| Object storage (EU S3-compatible) | Attachments + backups | Vendor DPA | _unsigned_ | |
| Postmark | Transactional email | https://postmarkapp.com/eu-privacy | _unsigned_ | |
| 46elks | SMS | 46elks DPA / processor terms | _unsigned_ | |
| Uptime (Better Stack / UptimeRobot) | External `/health/ready` | Vendor DPA | _unsigned_ | |
| Error tracking | Stack traces | GlitchTip self-host = none; Sentry EU if used | _pending choice_ | |
| Google Maps (if BrowserKey set) | Address UI | Google Cloud DPA / decide to keep Nominatim-only | _review_ | |

Do not enable a vendor in production until its row is signed and the filing path is filled in.

Related: [register-of-processing.md](register-of-processing.md).
