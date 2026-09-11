-- Expect equine_app to lack DELETE on immutable tables and to lack DROP.
-- Run as a superuser after scripts/prod-grants.sql.

SELECT table_name, privilege_type
FROM information_schema.role_table_grants
WHERE grantee = 'equine_app'
  AND table_name IN ('journal_entries', 'journal_amendments', 'audit_log')
  AND privilege_type IN ('DELETE', 'TRUNCATE', 'DROP')
ORDER BY 1, 2;

-- Zero rows = expected. Any DELETE/TRUNCATE/DROP row is a go-live blocker.
