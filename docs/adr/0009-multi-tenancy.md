# ADR-0009: Shared-database multi-tenancy

**Status:** Accepted  
**Date:** 2026-09-12  
**Subject:** How HästJournal isolates practices after becoming a SaaS platform

## Context

The product was a single-practice deployment. Practices must now self-register on one API and one PostgreSQL database, without leaking another clinic’s journals, bookings, or clients.

## Decision

Use a **shared database** with a `Tenant` row per practice and `TenantId` on every practice-owned table. EF Core global query filters hide other tenants. Public widget and portal identify the practice by **slug in the path** (`/widget/{slug}`, `/portal/{slug}`, `/api/public/{slug}/…`). One login belongs to one practice (`ApplicationUser.TenantId`). New tenants start on a **Free** plan; Stripe columns are reserved but unused.

## Consequences

### Positive
- Fits the existing modular monolith and one-Postgres hosting model.
- Existing data can be backfilled onto a default tenant.
- Public embed URLs stay on one host (no wildcard DNS).

### Negative
- Query filters must be applied everywhere; Hangfire and token routes set tenant explicitly.
- Identity emails stay globally unique, so a person cannot belong to two practices yet.

## Alternatives considered

1. **Database-per-tenant** — heavier ops than the current VPS/Dokploy setup.
2. **Subdomain per tenant** — nicer branding, but needs wildcard TLS and Traefik changes.
