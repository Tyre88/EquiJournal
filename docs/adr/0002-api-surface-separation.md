# ADR-0002: API Surface Separation — Public vs App

**Status:** Accepted  
**Date:** 2026-09-08  
**Subject:** Two distinct API surfaces with separate middleware pipelines

## Context

The system has two fundamentally different consumers:
1. An authenticated practitioner using the admin SPA (needs access to all data).
2. Anonymous visitors using the public booking widget (need minimal, rate-limited access).

These are a **security boundary** — data leaks between surfaces must be impossible.

## Decision

Two top-level route groups with separate middleware pipelines:

- `/api/public/*` — Anonymous, rate-limited (50 req/15s per window), CORS-controlled. Never returns owner names, horse names, or reasons for slot unavailability.
- `/api/app/*` — Requires JWT authentication. Anonymous access is opt-in per endpoint via `[AllowAnonymous]`.

## Consequences

### Positive
- Clear security boundary — middleware for rate limiting, CORS, and auth is wired per-surface.
- Easier to audit — public endpoints can be reviewed for data exposure independently.
- Rate limiting cannot be bypassed by authenticating.

### Negative
- Duplication of validation logic may arise for endpoints that conceptually exist on both surfaces (e.g., "get treatment types" — same data, different access levels).
- Requires discipline to never leak sensitive data through the public surface (e.g., "slot unavailable: because Anna's horse is booked" must not happen).

## Implementation Notes

- Rate limiter: fixed-window, 50 requests per 15-second window.
- CORS: configurable allowlist via `appsettings.json`.
- Auth: JWT bearer, applied via `[Authorize]` at the group level for `/api/app/*`.
