# Configuration reference

All values can be set as environment variables (`Section__Key`). Production: Dokploy env only. Never commit secrets.

| Key | Purpose | Default / safe range |
|---|---|---|
| `ConnectionStrings__Default` | Postgres | Required in Production. Local: `localhost:5432` / `equijournal` / `postgres` |
| `Jwt__Issuer` | Token issuer | `Equine.Api` |
| `Jwt__Audience` | Token audience | `EquineClient` |
| `Jwt__SecurityKey` | HMAC-SHA256 | **Required**, ≥ 32 chars, unique per environment |
| `Admin__Email` | Seeded admin if missing | `admin@equijournal.se` |
| `Admin__Password` | Seed password (12+ chars, complexity) | Dev only |
| `Admin__DisplayName` | Seed display name | `Admin` |
| `Cors__AllowedOrigins` | Admin/portal origins | Dev includes `:4200` and `:4201` |
| `Storage__ServiceUrl` | S3/MinIO endpoint | `http://localhost:9000` |
| `Storage__AccessKey` / `SecretKey` / `Bucket` | Object storage | Dev: minioadmin / `equine-attachments` |
| `Smtp__*` | Dev mail (Mailpit) | `localhost:1025` |
| `Postmark__ServerToken` / `From` / `FromName` | Production email | Empty in non-prod |
| `Elks__Username` / `Password` / `From` | Production SMS | Empty in non-prod |
| `Notifications__SendingDomain` | SPF/DMARC check | Empty skips |
| `GoogleMaps__BrowserKey` | Settings map | Empty = no Maps script |
| `RateLimiting__PublicReadPermitLimit` | Public GET | 50 / 15s |
| `RateLimiting__PublicReadWindowSeconds` | | 15 |
| `RateLimiting__PublicBookingPermitLimit` | Public book | 5 / 60 min |
| `RateLimiting__PublicBookingWindowMinutes` | | 60 |
| `RateLimiting__PublicBookingEmailPermitLimit` | Per email | 3 / hour |
| `Practitioner__*` | Fallback practice row | Used if DB empty |
| `ASPNETCORE_ENVIRONMENT` | | `Development` / `Production` / `Testing` |
| `ASPNETCORE_URLS` | Container bind | `http://+:8080` |

The Dokploy compose stack does not run Postgres. The API connects to `equijournal-production-lwqnp4:5432`, database `equijurnal`, user `equi`. The password is set on the api service in `docker-compose.dokploy.yml` so a stale `POSTGRES_PASSWORD` in the Dokploy UI is not sent. The API builds the Npgsql connection string from those parts. Local `docker-compose.yml` still runs its own Postgres.

HSTS max-age is 180 days in Production (raise after a clean month). Hangfire dashboard: `/hangfire`, admin only.
