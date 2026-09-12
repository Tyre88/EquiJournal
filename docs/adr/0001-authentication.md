# ADR-0001: Authentication Strategy — JWT Bearer Tokens

**Status:** Accepted  
**Date:** 2026-09-08  
**Subject:** How the API authenticates the Angular SPA

## Context

The system is a multi-tenant practice tool with an Angular SPA (admin app) and a public anonymous booking widget. Each staff user belongs to one tenant; JWTs carry a `tenantId` claim. The API must authenticate the practitioner without introducing session-cookies that complicate cross-origin deployment, while also supporting a public anonymous API surface.

## Decision

We use **JWT bearer tokens** (short-lived access tokens of 15 minutes + refresh token rotation) over cookie-based authentication.

## Consequences

### Positive
- Stateless authentication — scales trivially across replicas.
- SPA-friendly — tokens are stored in `localStorage` and sent as `Authorization` headers.
- Clean separation of public vs authenticated surfaces — the rate limiter and CORS policies apply at the route level, independent of auth mechanism.
- 2FA TOTP integration is straightforward — we validate the TOTP code after password verification but before issuing tokens.

### Negative
- Token revocation requires a deny-list or short TTL + refresh rotation (we use 15-minute access tokens).
- `localStorage` is vulnerable to XSS — the SPA must be free of XSS vulnerabilities. We mitigate with strict CSP headers (to be added in M5).
- No automatic expiry handling — the SPA must implement refresh token rotation.

## Alternatives Considered

1. **Cookie-based auth with SameSite:** Would work for same-origin deployments but complicates multi-origin setups (e.g., widget on a third-party site).
2. **IdentityServer/Duende:** Over-engineered for the current Identity + JWT setup. ASP.NET Identity + custom JWT is sufficient.

## Implementation Notes

- Access token: 15 minutes, signed with HMAC-SHA256.
- Refresh token: opaque string (Base64 of random bytes), rotated on each use.
- TOTP: 30-second window, 6 digits, Otp.NET library.
- Roles: `Admin`, `Practitioner`, `Assistant`, `Client`.
- 2FA is mandatory for `Admin` and `Practitioner` roles.
