-- Production grants. Run as a superuser / equine_migrate after schema exists.
-- equine_app: DML only. No DELETE on immutable journal/audit tables. No DROP/TRUNCATE.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'equine_migrate') THEN
        CREATE ROLE equine_migrate LOGIN PASSWORD 'change-me-migrate';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'equine_app') THEN
        CREATE ROLE equine_app LOGIN PASSWORD 'change-me-app';
    END IF;
END
$$;

-- GRANT CONNECT ON DATABASE equijournal TO equine_migrate, equine_app;
GRANT USAGE ON SCHEMA public TO equine_migrate, equine_app;

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO equine_migrate;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO equine_migrate;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO equine_migrate;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO equine_migrate;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO equine_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO equine_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO equine_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO equine_app;

REVOKE DELETE, TRUNCATE ON TABLE journal_entries FROM equine_app;
REVOKE DELETE, TRUNCATE ON TABLE journal_amendments FROM equine_app;
REVOKE DELETE, TRUNCATE ON TABLE audit_log FROM equine_app;

REVOKE CREATE ON SCHEMA public FROM equine_app;
