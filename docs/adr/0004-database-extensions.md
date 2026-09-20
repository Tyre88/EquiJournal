# ADR-0004: Database Extensions — citext, btree_gist, pg_trgm

**Status:** Accepted  
**Date:** 2026-09-08  
**Subject:** Required PostgreSQL extensions for the schema

## Context

Swedish business and legal requirements, combined with practical needs for a veterinary journal system, require:
- Case-insensitive email matching (GDPR/registration).
- Partial text search for journal content (Swedish legal requirement).
- Unique constraints covering partial matches.

## Decision

Enable three PostgreSQL extensions in the initial migration:

| Extension | Purpose |
|---|---|
| `citext` | Case-insensitive email/text columns |
| `pg_trgm` | Trigram-based text search for journal content |
| `btree_gist` | GIST index support for GiST operators on common types |

## Consequences

### Positive
- `citext` on email columns means `WHERE email = 'ADMIN@EQUISEED.SE'` works correctly.
- `pg_trgm` enables `%` operator for similarity searches — useful for finding journals by owner name or horse name.
- `btree_gist` enables GiST indexes on `uuid`, `timestamptz`, and other types.

### Negative
- Extensions are PostgreSQL-specific — migration to another RDBMS would require rewrites.
- `pg_trgm` index size grows with table size; must be monitored.

## Implementation Notes

- Extensions are enabled in the initial EF Core migration (`20260920074046_InitialCreate`).
- Email columns should be declared as `citext` in EF Core mappings.
