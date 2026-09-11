# ADR-0007: Public widget is an iframe, not Angular Elements

**Status:** Accepted  
**Date:** 2026-09-10  
**Subject:** Isolation model for the public booking widget

## Context

The booking widget is embedded on third-party sites (typically WordPress with unpredictable themes and plugins). Two delivery options were considered:

1. Angular Elements / custom element, running in the host page.
2. A tiny loader that injects an iframe pointing at our hosted widget.

## Decision

Ship an iframe via `/widget/v1/loader.js`. The widget Angular app lives on our origin. A custom-element build is deferred until a specific site needs inline styling control.

## Consequences

### Positive
- Complete CSS and JS isolation from host themes.
- Host sites do not need CSP changes to allow our scripts to style their page.
- Widget upgrades are transparent: the iframe loads the current app.
- `frame-ancestors` on `/widget` plus an origin allowlist enforce which sites may embed us.

### Negative
- Host pages cannot restyle the widget internals.
- The loader must resize the iframe via `postMessage` (origin-checked).

CORS on `/api/public` is not the embed allowlist. In production the widget and API share one origin; the allowlist drives `frame-ancestors` and the loader's `postMessage` check.
