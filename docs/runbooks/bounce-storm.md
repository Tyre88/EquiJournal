# Respond to a bounce storm

## Symptoms

- Spike in Postmark bounces or complaints
- `NotificationLog` rows with failed delivery
- Practitioner receives `NotificationFailed` alerts

## Immediate actions

1. Pause marketing sends: do not use `/api/app/marketing/send` until resolved.
2. Check Postmark activity — identify the campaign or notification type.
3. Mark invalid addresses: set `EmailInvalid = true` on affected owners.
4. Verify SPF/DKIM/DMARC per [email-deliverability.md](./email-deliverability.md).

## Investigation

```sql
SELECT "Recipient", "Type", "Status", "CreatedAt"
FROM notification_log
WHERE "CreatedAt" > now() - interval '24 hours'
  AND "Status" IN ('Bounced', 'Failed')
ORDER BY "CreatedAt" DESC;
```

## Recovery

1. Fix template or recipient list issue.
2. Re-enable transactional notifications only (reminders, confirmations).
3. Monitor bounce rate for 48 hours before resuming marketing.
