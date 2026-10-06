# ADR-0005: Docker Build Strategy — Single Image with Served Assets

**Status:** Accepted  
**Date:** 2026-09-08  
**Subject:** Container strategy for the monolith

## Context

The system is a modular monolith with:
- An ASP.NET Core backend (API + Angular asset serving).
- PostgreSQL (external managed service in production).
- Angular SPAs (admin, widget, portal).

Options:
1. Single Docker image with ASP.NET hosting Angular assets.
2. Separate images for API and each Angular app.
3. Separate image for API, reverse proxy (nginx) for Angular apps.

## Decision

**Single Docker image.** ASP.NET Core serves the Angular apps from `/wwwroot/admin`, `/wwwroot/widget`, and `/wwwroot/portal`, plus the widget loader.

## Consequences

### Positive
- Simplest deployment — one container, one image, one health check.
- No need for nginx or reverse proxy configuration.
- ASP.NET's static file middleware can serve SPA routes with fallback to `index.html`.

### Negative
- Not ideal for high-traffic static content — ASP.NET is not a dedicated static file server.
- Admin, widget, and portal share one container.

## Implementation Notes

- Build Angular apps in a multi-stage Docker build (`npm run build` in `web/`).
- Copy output to `/wwwroot/admin`, `/wwwroot/widget`, and `/wwwroot/portal`.
- ASP.NET serves static files from these directories.
- Health endpoint: `/health` and `/health/ready`.
