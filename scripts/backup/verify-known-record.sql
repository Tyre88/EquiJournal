-- Verify a known journal after restore. Replace the id before running.
-- psql "$CONN" -v journal_id="'00000000-0000-0000-0000-000000000000'" -f scripts/backup/verify-known-record.sql

\echo '=== journal ==='
SELECT "Id", "HorseId", "PerformedAt", "Status", "SignedBy", "ContentHash",
       left("Anamnes", 80) AS anamnes_preview,
       "Source"
FROM journal_entries
WHERE "Id" = :journal_id;

\echo '=== amendments ==='
SELECT "Id", "CreatedAt", left("Text", 80) AS text_preview
FROM journal_amendments
WHERE "JournalEntryId" = :journal_id
ORDER BY "CreatedAt";

\echo '=== attachments ==='
SELECT "Id", "OriginalFileName", "ContentType", "Size", "StorageKey", "Sha256Checksum"
FROM attachments
WHERE "JournalEntryId" = :journal_id
ORDER BY "CreatedAt";
