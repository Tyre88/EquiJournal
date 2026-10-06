# External Postgres

`docker-compose.dokploy.yml` does not run Postgres. The API connects to an external database:

| Setting | Value |
|---|---|
| Host | `equijournal-production-lwqnp4` |
| Port | `5432` |
| Database | `equijurnal` |
| User | `equi` |

`Postgres__Password` is set on the `api` service in that compose file. The API builds the Npgsql connection string from `Postgres__Host`, `Postgres__Port`, `Postgres__Database`, `Postgres__Username`, and `Postgres__Password`. A leftover `POSTGRES_PASSWORD` in the Dokploy UI is not the credential the API sends. See [configuration.md](../configuration.md).

## If the API logs SqlState 28P01

The host accepted the connection and rejected the password for `equi`. Compare those `Postgres__*` values on the `api` service with the role on `equijurnal`.

This stack has no bundled `postgres` service, `postgres_data` volume, or password-sync command. Do not alter a `postgres` role or delete a compose volume to fix it.

Local `docker-compose.yml` is a different database: `localhost:5432`, database `equijournal`, user `postgres`.
