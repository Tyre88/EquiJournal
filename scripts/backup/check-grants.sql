-- Expect the live app role equi to lack DELETE and TRUNCATE on immutable
-- journal/audit tables. Run as a superuser after scripts/prod-grants.sql.
--
-- information_schema.role_table_grants never returns DROP. Its privilege_type
-- values are SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER.
-- DROP TABLE follows ownership, not a grant this view can show.
--
-- Zero rows = expected. Any DELETE or TRUNCATE row is a go-live blocker.

SELECT table_name, privilege_type
FROM information_schema.role_table_grants
WHERE grantee = 'equi'
  AND table_name IN ('journal_entries', 'journal_amendments', 'audit_log')
  AND privilege_type IN ('DELETE', 'TRUNCATE')
ORDER BY 1, 2;
