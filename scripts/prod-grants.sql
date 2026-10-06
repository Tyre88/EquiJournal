-- Least-privilege grants for the live Dokploy login.
-- Database: equijurnal
-- Role: equi (Postgres__Username in docker-compose.dokploy.yml)
--
-- The API connects as equi. This script does not create roles and does not
-- set passwords. equine_app / equine_migrate are not used by the stack.
-- Applying it revokes DELETE and TRUNCATE on the journal/audit tables, and
-- CREATE on schema public, from that login. It does not run on deploy.
-- Run as a superuser on the cluster that contains equijurnal, after the
-- schema exists. Local docker-compose uses database equijournal instead.

GRANT CONNECT ON DATABASE equijurnal TO equi;
GRANT USAGE ON SCHEMA public TO equi;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO equi;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO equi;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO equi;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO equi;

REVOKE DELETE, TRUNCATE ON TABLE journal_entries FROM equi;
REVOKE DELETE, TRUNCATE ON TABLE journal_amendments FROM equi;
REVOKE DELETE, TRUNCATE ON TABLE audit_log FROM equi;

REVOKE CREATE ON SCHEMA public FROM equi;
