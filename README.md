# HästJournal

Ett boknings- och journalsystem för djurbehandling, byggt enligt svensk lagstiftning (lagen 2009:302, SJVFS 2023:19).

## Snabbstart

Krav: .NET 10 SDK, Node.js 22, Docker. Mål: ny person igång på under 30 minuter.

### 1. Starta lokal infrastruktur

```bash
docker compose up -d postgres mailpit minio minio-init
```

Postgres (`localhost:5432`, databas `equijournal`, användare/lösen `postgres`), Mailpit (`localhost:8025`) och MinIO (`localhost:9000`, bucket `equine-attachments`) måste köra. Utan MinIO misslyckas bilagor.

### 2. API

```bash
cd src/Equine.Api
dotnet run
```

API: `http://localhost:5087`  
Swagger: `http://localhost:5087/swagger`  
Liveness: `http://localhost:5087/health`  
Readiness: `http://localhost:5087/health/ready`

### 3. Admin

```bash
cd web
npm install
npm run serve:admin
```

Admin: `http://localhost:4200`

### 4. Widget och portal (valfritt)

```bash
npm run serve:widget   # http://localhost:4201
npm run serve:portal
```

### Standard-inloggning (Development)

E-post: `victor@gradera.nu`  
Lösenord: `Admin@123456`

Överskrivs med `Admin__Email` / `Admin__Password` / `Admin__DisplayName`. Befintlig admin raderas inte vid omstart.

Aktivera 2FA under Inställningar → Konto.

### API-klient

Om du ändrar endpoints: starta API:et och kör `npm run generate:api` i `web/`. Committa `libs/api-client`.

## Testa

```bash
dotnet test
dotnet test tests/Equine.UnitTests/
dotnet test tests/Equine.IntegrationTests/
```

Integrationstester kräver Docker (Testcontainers).

## Historisk import

CSV-mallar i `docs/import/`. Dry-run är standard:

```bash
dotnet run --project tools/Equine.Import -- --owners owners.csv --horses horses.csv --journals journals.csv
```

## Arkitektur och drift

- [docs/architecture.md](docs/architecture.md)
- [docs/adr/README.md](docs/adr/README.md)
- [docs/configuration.md](docs/configuration.md)
- [docs/deploy-and-rollback.md](docs/deploy-and-rollback.md)
- [docs/manual/anvandarmanual.md](docs/manual/anvandarmanual.md)
- Runbooks: restore, backup, monitoring, key rotation, provider outage, bounce storm, stuck Hangfire, patching

| Lager | Ansvar |
|---|---|
| `Equine.Domain` | Entiteter och regler |
| `Equine.Infrastructure` | EF Core, lagring, e-post, import, export |
| `Equine.Api` | Minimal API |
| `Equine.Jobs` | Hangfire |
| `tools/Equine.Import` | CSV-import |
| `web/apps/admin` | Praktiker |
| `web/apps/widget` | Publik bokning |
| `web/apps/portal` | Klientportal |

## Miljöer

- **Development:** `appsettings.Development.json` (localhost). Inga hemligheter i `appsettings.json`.
- **Production:** enbart miljövariabler i Dokploy. `EnsureCreated` körs inte.

## CI/CD

GitHub Actions körs från [`.github/workflows/ci.yml`](.github/workflows/ci.yml) på push och pull requests mot `main`.

| Jobb | När | Vad |
|------|-----|-----|
| **validate** | Varje PR och push | `dotnet format`, .NET build, domänisolering, enhetstester, Angular-build, widget-loader |
| **publish** | Push till `main` | Docker-build (ingen push till registry ännu) |

**Medvetet utanför första återaktiveringen:** integrations­tester (Testcontainers/Docker) och `ng lint` (saknar `@angular-eslint`-paket). Se workflow-kommentarer och PR-beskrivning.

E-post-DNS kontrolleras även veckovis via [`.github/workflows/deliverability-dns.yml`](.github/workflows/deliverability-dns.yml) (inte på varje PR).

Dependabot körs veckovis via [`.github/dependabot.yml`](.github/dependabot.yml).

## License

Private project — Skåne, Sweden.
