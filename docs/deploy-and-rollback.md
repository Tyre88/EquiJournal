# Deploy and rollback (Dokploy + Traefik)

## Build

CI builds the image from [Dockerfile](../Dockerfile) (API + admin + widget + portal + loader). Push to GHCR when you enable `push: true` / a release workflow. Tag with git SHA or semver.

## Dokploy

1. Application from the GHCR image, port **8080**.
2. Environment variables only — never bake secrets into the image. See [configuration.md](configuration.md).
3. Domain + HTTPS (Let’s Encrypt) on Traefik. Force TLS 1.2+ on the entrypoint.
4. Health check: `GET /health/ready`.
5. Data Protection keys: persist a volume (magic links break if keys rotate unexpectedly).
6. Postgres and object storage as separate Dokploy services. App user: `equine_app` after [scripts/prod-grants.sql](../scripts/prod-grants.sql). Schema changes: `equine_migrate` or superuser, then restart the app. **Production does not run `EnsureCreated`.**

## Rollback

1. In Dokploy, deploy the **previous image tag**.
2. If a schema change already ran, restore the last dump to staging first; only then decide whether production needs a dump restore.
3. Confirm `/health/ready` and one admin login + today’s schema.

## Troubleshooting

- API crash-loops with `SqlState: 28P01` / `password authentication failed`:
  [runbooks/postgres-auth-failure.md](runbooks/postgres-auth-failure.md).

## Cutover

See [cutover.md](cutover.md).
