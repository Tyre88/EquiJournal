# Revoke a magic link

Magic links are single-use and expire after 15 minutes. To revoke access immediately:

## Client session (JWT)

1. Identify the owner's linked `ApplicationUser` in the database.
2. Increment security stamp or delete refresh tokens to invalidate JWT:
   ```sql
   UPDATE "AspNetUsers" SET "SecurityStamp" = gen_random_uuid()::text WHERE "Id" = '<user-id>';
   ```
3. Client must request a new magic link to sign in again.

## Pending magic link (not yet exchanged)

- Links expire automatically after 15 minutes.
- After exchange, the token hash is stored in `consumed_magic_link_tokens` and cannot be reused.

## Suspected account compromise

1. Revoke JWT as above.
2. Verify owner email/phone in admin.
3. Review `audit_log` for `LOGIN` and portal access on that owner's horses.
