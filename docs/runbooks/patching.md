# Patching cadence

- **Weekly:** review Dependabot PRs (NuGet, npm in `web/` and `widget-loader/`, GitHub Actions). Merge routine bumps after CI is green.
- **Critical / high** vulnerabilities: patch or mitigate within **48 hours**. If a bump breaks the build, pin and open a follow-up issue the same day.
- **Monthly:** `dotnet list package --outdated` and `npm outdated` in `web/`. Apply non-breaking updates in one PR.
- CI **runs** `dotnet list package --vulnerable` and `npm audit --omit=dev --audit-level=high` and surfaces them as warnings (some transitive packages on .NET 10.0.0 still report advisories). Drive those to zero in the weekly review; do not ignore a new critical without a ticket.
- After production deploys, watch `/health/ready` and Hangfire for 15 minutes.
