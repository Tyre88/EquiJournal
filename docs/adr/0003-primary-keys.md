# ADR-0003: Primary Key Strategy — UUID v7

**Status:** Accepted  
**Date:** 2026-09-08  
**Subject:** All primary keys are UUID version 7

## Context

The system needs globally unique identifiers that:
1. Are safe to expose (no auto-incrementing integers that reveal record counts).
2. Have good database index performance (sequential-ish generation reduces fragmentation).
3. Can be generated in the application without round-trips to the database.

## Decision

Use **UUID v7** (time-sortable, RFC 9562) generated in application code via `Guid.CreateVersion7()`.

## Consequences

### Positive
- Sortable by creation time — useful for listing queries without an extra timestamp column.
- No auto-increment — cannot infer record counts.
- EF Core configured with `ValueGeneratedOnAdd` but application generates the value.
- Compatible with PostgreSQL's `uuid` type and index structures.

### Negative
- Larger than integer-based keys (16 bytes vs 4 bytes), slightly more storage and index overhead.
- .NET 8+ supports `Guid.CreateVersion7()` natively; requires .NET 10 runtime.

## Implementation Notes

- All entities inherit from `Entity` which sets `Id = Guid.CreateVersion7()` in the constructor.
- PostgreSQL does not auto-generate UUIDs — the application always sets the Id before saving.
