# Rotate provider keys

## Postmark (email)

1. Create a new Server API token in Postmark.
2. Update `Postmark:ServerToken` in the deployment secret store.
3. Deploy the API (rolling restart).
4. Send a test email from staging.
5. Revoke the old token in Postmark.

## 46elks (SMS)

1. Generate a new API password in 46elks.
2. Update `Elks:ApiPassword` in secrets.
3. Deploy and send a test SMS.
4. Disable the old credentials.

## JWT signing key

1. Generate a new `Jwt:SecurityKey` (min 32 characters).
2. Deploy — existing sessions will be invalidated; users must log in again.
3. Update portal magic-link tokens are unaffected (Data Protection keys are separate).

## Data Protection keys

Persist keys to a shared volume in production. When rotating, plan for invalid magic links until clients request new ones.
