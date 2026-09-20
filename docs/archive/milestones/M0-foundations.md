# M0 — Foundations

**Goal:** an empty but complete skeleton that builds, tests, and deploys to staging. Nothing in this milestone is user-visible except a login screen.
**Estimated effort:** 2 weeks
**Prerequisite:** read `00-CONTEXT.md`.

> **Do not** implement horses, owners, journals, bookings or treatment types in this milestone. Only the plumbing.

---

## Scope

### In
- Repository, solution and Angular workspace structure
- PostgreSQL + EF Core wiring, first migration, Testcontainers integration test harness
- Authentication and authorisation skeleton
- OpenAPI → Angular client generation pipeline
- Logging, health checks, configuration, secrets handling
- CI pipeline and deployment to a staging environment

### Out
- Any domain feature
- The widget app beyond an empty shell
- Notifications, background jobs beyond a no-op ticker

---

## Tasks

### 1. Solution scaffolding
Create the solution and projects exactly as laid out in `00-CONTEXT.md`. `Equine.Domain` must have **no** EF Core or ASP.NET dependency — enforce this with a project reference check in CI.

### 2. Database and EF Core
- `EquineDbContext` in `Equine.Infrastructure`, registered with Npgsql.
- Enable extensions in the first migration: `citext`, `btree_gist`, `pg_trgm`.
- Configure global conventions: `DateTimeOffset` → `timestamptz`, UUID v7 default generation in application code, snake_case table and column naming.
- A `SoftDeletableEntity` base and a global query filter for `deleted_at IS NULL`.
- Seed only a single admin user (from configuration, not hard-coded).

### 3. Audit log
Create the `audit_log` table and an `IAuditWriter` service: actor id, action, entity type, entity id, timestamp, `before`/`after` JSONB diff. Nothing writes to it yet, but the mechanism exists and is tested.

### 4. Authentication
- ASP.NET Core Identity with a custom `ApplicationUser`, backed by PostgreSQL.
- JWT bearer for the SPA (short-lived access token + refresh token rotation), or cookie auth — pick one, write an ADR in `/docs/adr/` explaining the choice.
- TOTP two-factor: enrolment and challenge endpoints. **2FA is mandatory for any role that can read journals** — enforce with a policy now even though journals don't exist yet.
- Roles: `Admin`, `Practitioner`, `Assistant`, `Client`. Register authorisation policies: `CanReadJournals`, `CanWriteJournals`, `CanManageBookings`, `CanAdminister`.
- A `/api/app/me` endpoint returning the current user, roles and 2FA status.

### 5. API surface separation
- Two endpoint groups: `/api/public` and `/api/app`, with distinct middleware pipelines.
- `/api/public` gets `AddRateLimiter` (per-IP fixed window) and a CORS policy driven by a configurable origin allowlist.
- `/api/app` requires authentication by default; anonymous access must be opt-in per endpoint.
- One trivial endpoint in each group (`/api/public/ping`, `/api/app/me`) to prove the split works.

### 6. Cross-cutting
- Serilog: console (JSON in production) + rolling file. Correlation id per request.
- `/health` (liveness) and `/health/ready` (database reachable).
- `ProblemDetails` for all error responses; a global exception handler that never leaks stack traces in production.
- Configuration via `appsettings` + environment variables. User secrets in development. **No secret is ever committed.**
- FluentValidation registered and wired into the Minimal API pipeline.

### 7. Angular workspace
- `web/` workspace with `apps/admin`, `apps/widget`, `libs/api-client`, `libs/ui`.
- Standalone bootstrap, zoneless change detection, `LOCALE_ID = 'sv-SE'` with `registerLocaleData(localeSv)`.
- Admin app: login page, 2FA challenge page, empty authenticated shell with navigation placeholder, auth interceptor and guard, token refresh handling.
- Widget app: empty shell that renders "Bokning kommer snart" — proves the build and deploy path.
- Shared `libs/ui`: layout shell, button/input/dialog primitives, Swedish date pipe, ISO week helpers.

### 8. API client generation
- `npm run generate:api` invokes `ng-openapi-gen` (or `openapi-generator`) against the running API's OpenAPI document and writes into `libs/api-client`.
- Output is **committed**. CI fails if the generated client is out of date with the spec.

### 9. CI/CD
GitHub Actions workflow:
1. Restore, build, `dotnet format --verify-no-changes`
2. Unit tests
3. Integration tests (Testcontainers PostgreSQL)
4. Angular lint + build both apps
5. Verify generated API client is current
6. Build Docker image (API + served Angular assets, or separate images — document the choice)
7. On tag: deploy to staging

Provide a `docker-compose.yml` for local development: API, PostgreSQL, MailHog (or Mailpit), MinIO.

### 10. Documentation
- `README.md`: how to run locally in under five minutes.
- `/docs/adr/0001-*.md` onward for each significant choice.

---

## Acceptance criteria

- [ ] `docker compose up` then one command starts the whole stack locally
- [ ] `dotnet test` passes, including at least one integration test that hits a real PostgreSQL container
- [ ] An admin user can log in, is forced through TOTP enrolment, and reaches the empty shell
- [ ] A request to `/api/app/me` without a token returns 401; with a token returns the user
- [ ] `/api/public/ping` is reachable from an allowlisted origin and blocked from a non-allowlisted one
- [ ] Rate limiter returns 429 after the configured threshold on `/api/public/*`
- [ ] `Equine.Domain` compiles with no EF Core or ASP.NET reference, verified in CI
- [ ] Pushing a tag deploys to staging and `/health/ready` returns healthy
- [ ] Generated API client is committed and CI verifies it matches the spec

## Definition of done

Staging is live, reachable over HTTPS, and a login works against it. Every subsequent milestone deploys through this same pipeline without modification.
