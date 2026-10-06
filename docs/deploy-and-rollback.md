# Deploy and rollback (Dokploy + Traefik)

## Build

Dokploy builds [Dockerfile](../Dockerfile) from [docker-compose.dokploy.yml](../docker-compose.dokploy.yml) (`api.build`). The image serves the API, admin, widget, portal, and the widget loader. No CI job builds or pushes that image to GHCR.

## Dokploy

1. Compose application from `docker-compose.dokploy.yml`. The API listens on port **8080**; Traefik labels on `api` publish the domain.
2. Environment variables only — never bake secrets into the image. See [configuration.md](configuration.md).
3. Domain + HTTPS (Let’s Encrypt) on Traefik. Force TLS 1.2+ on the entrypoint.
4. Health check: `GET /health/ready`.
5. Data Protection keys: persist a volume (magic links break if keys rotate unexpectedly).
6. Database is external: host `equijournal-production-lwqnp4`, database `equijurnal`, user `equi` (`Postgres__*` on `api`). Object storage is MinIO in the same compose file. Least-privilege grants for `equi`: [scripts/prod-grants.sql](../scripts/prod-grants.sql). **Production does not run `EnsureCreated`.**

## Rollback

1. In Dokploy, redeploy the previous successful deployment of this compose file.
2. If a schema change already ran, restore the last dump to staging first; only then decide whether production needs a dump restore.
3. Confirm `/health/ready` and one admin login + today’s schema.

## Troubleshooting

- API logs `SqlState: 28P01` / `password authentication failed` for user `equi`:
  [runbooks/postgres-auth-failure.md](runbooks/postgres-auth-failure.md).

## Cutover

See [cutover.md](cutover.md).
