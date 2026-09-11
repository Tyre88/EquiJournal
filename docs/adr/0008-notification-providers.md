# ADR-0008: Notification providers and job runner

**Status:** Accepted  
**Date:** 2026-09-10  
**Subject:** Email, SMS and background jobs for transactional notifications

## Context

M4 requires reliable transactional email and SMS, a durable queue, and several scanners. Provider choice affects deliverability, cost and EU data handling.

## Decision

- **Email (Production):** Postmark HTTP API. Transactional reputation and EU sender support.
- **Email (Development):** SMTP to Mailpit. Never a production provider from Development.
- **Email / SMS (Staging, Testing):** in-memory recording senders registered at the composition root. Production keys in config cannot send.
- **SMS (Production):** 46elks. Swedish domestic traffic, simple HTTP API.
- **Jobs:** Hangfire with PostgreSQL storage. Claiming due `scheduled_notifications` still uses `SELECT … FOR UPDATE SKIP LOCKED` so a double wake cannot double-send.

## Consequences

Hangfire dashboard lives at `/hangfire` and requires the Admin role. Staging never talks to Postmark or 46elks regardless of configuration.
