# Data breach response

Personal data breach: accidental or unlawful destruction, loss, alteration, unauthorised disclosure of or access to personal data.

## 72 hours

If the breach is likely to result in a risk to individuals, **notify IMY within 72 hours** of becoming aware of it. If high risk, notify affected owners as well.

IMY: https://www.imy.se/ — use their current breach form.

## Who does what

| Step | Who | When |
|---|---|---|
| Contain (revoke keys, take app read-only, rotate JWT / provider tokens) | Operator | Immediate |
| Preserve logs (Serilog files, audit_log, Hangfire, Traefik) | Operator | Immediate — do not wipe the box |
| Decide risk + IMY | Practitioner + operator | Within 24h assess, 72h notify |
| Tell affected owners | Practitioner | If high risk |
| Post-incident write-up | Operator | Within a week |

## Containment shortcuts

- [rotate-provider-keys.md](../runbooks/rotate-provider-keys.md)
- [revoke-magic-link.md](../runbooks/revoke-magic-link.md)
- Disable widget origins in widget settings
- Dokploy: stop the app or roll back

## Do not

- Email journal dumps to personal Gmail as a “backup”
- Restore over production before a copy of the current disk exists
