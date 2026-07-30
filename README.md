# Summer EBT (SUN Bucks) Self-Service Portal

[![State CI](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml/badge.svg)](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml)

## Background

The Summer EBT (SUN Bucks) Self-Service Portal is an application that allows parents/guardians
of children eligible for [Summer EBT](https://www.fns.usda.gov/summer/sunbucks) manage their benefit, including the following core features:

- Verifying a child's eligibility
- Verifying when and how the benefit will be received (which EBT card)
- Changing mailing address on file
- Requesting a replacement EBT card

## Repository layout (monorepo)

This is a monorepo containing the portal app and the shared + CO state connectors. The DC connector
remains an external repo (`sebt-self-service-portal-dc-connector`).

```
apps/
  portal/                 # the deployable portal: .NET API + Next.js web + enrollment checker
    src/, test/, SEBT.Portal.sln
  connectors/
    state/                # MEF plugin contract (interfaces), NuGet-packaged for external consumers
    co/                   # Colorado connector implementation
    dc/                   # placeholder README — the DC connector lives in its external repo
packages/                 # shared JS libraries: @sebt/design-system, @sebt/analytics
scripts/                  # repo-wide dev, CI, and git helper scripts
tofu/                     # infrastructure (OpenTofu)
SEBT.slnx                 # top-level solution: portal + in-repo connectors
```

Repo-wide config lives at the root: `pnpm-workspace.yaml`, `package.json`, `nuget.config`,
`global.json`, `Directory.Build.props`, and `.github/`.

### Local development

- **Build everything (root):** `dotnet build SEBT.slnx` for the backend; `pnpm ci:build:frontend`
  and `pnpm ci:build:enrollment-checker` for the web apps (the enrollment checker needs
  `NEXT_PUBLIC_*` values at build time, which those scripts provide).
- **Run for a state:** `pnpm dev:dc` or `pnpm dev:co` (builds the relevant connector plugins, then
  starts the API + web). DC uses the external `dc-connector` checked out alongside this repo; CO and
  the state contract build from `apps/connectors/*`.
- **Windows:** enable long paths (`git config core.longpaths true`) — the nested `apps/portal/...`
  paths can exceed the legacy 260-char limit.

### CI/CD (high level)

- `state-ci.yaml` builds/tests the portal + connectors on PRs and pushes. PRs are path-filtered:
  portal-only changes skip connector-irrelevant jobs and vice versa; pushes always run everything.
- `deploy-ecr.yaml` builds Docker images and deploys **DC** and **CO** to their dev environments;
  it builds the in-repo state + CO connectors and checks out the external DC connector.
- `release-iis-dc.yaml` produces the DC IIS release bundle (validated on every PR).
- `deploy-enrollment-checker.yaml` builds and deploys the static enrollment checker.
- `playwright-e2e.yaml` runs Playwright E2E (per state) and Pa11y accessibility checks.
- `build-and-seed-dc-source.yaml` builds the DC seed/source image from the external DC repo.

## Technology Stack overview

**Backend**

- Language/framework: [C# with .NET 10](https://dotnet.microsoft.com/en-us/languages/csharp)
- Key libraries: [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet), [Serilog](https://serilog.net/), [Managed Extensibility Framework (MEF)](https://learn.microsoft.com/en-us/dotnet/standard/mef/), [EntityFramework (EF) Core](https://learn.microsoft.com/en-us/ef/core/)
- Package manager: [NuGet](https://www.nuget.org/)

**Frontend**

- Language/framework: [NextJS 16](https://nextjs.org/) with TypeScript
- Key libraries: next, react, [i18next](https://www.i18next.com/), react-i18next, tanstack/react-query, zod
- Package manager: [pnpm](https://pnpm.io/)
- Design system: [USWDS](https://designsystem.digital.gov/), with design tokens specified for each state

**Infrastructure**

- Infrastructure as Code using OpenTofu (Terraform) - see [tofu](./tofu/)
- Docker with [docker-compose](https://docs.docker.com/compose/) for local development

## Local Environment Set Up 🧰

> **Note:** The following steps assume you are working on macOS. Steps may differ if you are working on a different operating system.

### 1. Make sure you have downloaded and installed prequisite software 👷

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) for running the back end
- The latest version of [nodeJS](https://nodejs.org/en)
- [pnpm](https://pnpm.io/installation/) for managing front end packages and development scripts
- [Docker](https://www.docker.com/) Desktop for running and managing containers (includes MSSQL database)

### 2. Clone the repository

The portal, the plugin contract, and the Colorado connector all live in this monorepo — one clone
covers CO development end to end:

```bash
git clone https://github.com/codeforamerica/sebt-self-service-portal.git
```

**Working on DC?** The DC connector is maintained in its own repository (see
[apps/connectors/dc/README.md](./apps/connectors/dc/README.md)). Clone it as a sibling (same parent
folder) so `pnpm dev:dc` can build it:

```bash
git clone https://github.com/codeforamerica/sebt-self-service-portal-dc-connector.git
```

> The old standalone `-state-connector` and `-co-connector` repositories were merged into this repo
> (`apps/connectors/`) and are archived — don't clone them for new work.

### 3. Configure local environment

`.env` files are used in this project to set environment variables (eg, database configs). This is a preferred pattern for [12-factor Apps](https://www.12factor.net/config). They are also set to fallback to a generic default. You'll need to create `.env` files for your local environment, based on the example file.

To create your local .env file with configurations for the database and API, run this command in the root of the repo:

```bash
cp .env.example .env
```

You'll want do the same from within `apps/portal/src/SEBT.Portal.Web`:

```bash
cp .env.example .env.local
```

You'll also need an API `appsettings` file for your local machine with certain values set (see [state specific configuration](#state-specific-configuration) below):

```bash
cd apps/portal/src/SEBT.Portal.Api
cp appsettings.Development.example.json appsettings.Development.json
```

### 4. Install dependencies

Front end

- To install all javascript package dependencies, run `pnpm install` from the root of this repository.
- You can learn more about the front end in the [SEBT.Portal.Web README](./apps/portal/src/SEBT.Portal.Web/README.md)

Back end

- .NET tools are CLI utilities installed and managed using [NuGet](https://www.nuget.org/). Currently, we are using the
  [`nuget-license`](https://www.nuget.org/packages/nuget-license) tool for auditing backend dependency license. Needed tools are defined in the tools manifest in `.config/dotnet-tools.json`. To install them, run `dotnet tool restore` once from the repo root.
- You'll also want to run `dotnet build SEBT.slnx` from the repo root before starting up the app for the first time — it builds the portal and the in-repo connectors together.

### 5. Start Services 💻

Make sure Docker is installed and the docker daemon is running. When the database spins up locally, all migrations will be run and db seeded automatically (see [database setup](#database-setup) section below).

```bash
docker compose up -d  # Start all docker containers, including MSSQL Database and Mailpit for testing
```

```bash
pnpm dev              # Script to start both API (ie, `dotnet watch`) and frontend (ie, `next dev`)
```

To open the app, navigate to <https://localhost:3000>

## Development

### Other helpful commands

```bash
# Start frontend only
pnpm web:dev

# View logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (clears database - do this only if you're OK with dropping your seeded data)
docker compose down -v
```

### Mailpit (Local Email Testing)

[Mailpit](https://mailpit.axllent.org/) captures all outgoing emails in local development. Once the Mailpit docker container is running on your machine, you can access its UI in your browser at <http://localhost:8025>

### Redis (Distributed Cache)

[Redis](https://redis.io/) is used as an optional distributed cache backing for `HybridCache`. It's included in Docker Compose and runs with TLS enabled to mirror AWS Elasticache in-transit encryption.

#### First-time setup

Before running `docker compose up` for the first time, generate the local TLS certificates:

```bash
./scripts/dev/gen-redis-certs.sh
```

This writes self-signed certs to `certs/` (gitignored). The script is idempotent — existing certs are not overwritten. Re-run it if Redis TLS stops working (certs expire after one year).

#### Ports

| Port | Protocol | Used by |
|------|----------|---------|
| 6379 | plain    | `redis-commander`, direct `redis-cli` |
| 6380 | TLS      | portal API |

#### Configuration

Add the following to your local `appsettings.{state}.json` to connect to the TLS port:

```json
"Redis": {
  "Host": "localhost",
  "Port": 6380,
  "Ssl": true,
  "SslHost": "redis",
  "AcceptSelfSignedCertificates": true
}
```

`SslHost` should match the hostname in the server certificate — `redis` locally (the Docker service name), or the Elasticache cluster endpoint in production. An optional `Password` field supports Redis AUTH tokens.

`AcceptSelfSignedCertificates: true` bypasses CA trust for the local self-signed cert. **Never set it in production** — Elasticache presents an AWS-signed cert that .NET trusts natively. See `appsettings.co.example.json` for the full example.

The legacy `ConnectionStrings:Redis` connection string is still accepted as a fallback, but new deployments should use the structured form.

When neither is configured, the application falls back to in-memory caching only. See `appsettings.co.example.json` for a full example.

### Jaeger (Local OpenTelemetry Tracing)

[Jaeger](https://github.com/jaegertracing/jaeger) acts as a local OTLP collector for OpenTelemetry tracing. The default configuration for the portal sends traces and metrics via OTLP over gRPC to http://localhost:4317, which is the standard port. Local traces can be viewed in the Jaeger UI at [http://localhost:16686](http://localhost:16686).

The Next.js web apps (`SEBT.Portal.Web`, `SEBT.EnrollmentChecker.Web`) also emit OTLP here, but only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set — they stay inert otherwise. `.env.example` points it at `http://localhost:4317`; copy that into `.env.local` to see web-tier traces alongside the API's.

### Local Build & Test (Debug mode)

```bash
pnpm api:build        # Build backend only (Debug)
pnpm api:test         # Test backend only
```

### CI Build & Test (Release mode)

```bash
pnpm ci:build         # Build frontend + backend (Release)
pnpm ci:test          # Test frontend + backend

# Individual components
pnpm ci:build:frontend   # Build frontend only
pnpm ci:build:backend    # Build backend only
pnpm ci:test:frontend    # Test frontend only
pnpm ci:test:backend     # Test backend only
```

### Warnings as errors

The .NET solution is configured with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`. Any compiler warning will fail the build.

If you need to allow a specific warning code, demote it back to a warning in the affected `.csproj`:

```xml
<PropertyGroup>
  <WarningsNotAsErrors>$(WarningsNotAsErrors);CS1591</WarningsNotAsErrors>
</PropertyGroup>
```

Prefer this over `<NoWarn>`, which silences the warning entirely.

### CI Testing (Local)

```bash
# State-based CI testing
pnpm ci:test:states   # Test all states
pnpm ci:test:state:dc # Test DC state
pnpm ci:test:state:co # Test CO state

# Utility commands
pnpm ci:list          # List all ACT workflows
pnpm ci:validate      # Validate workflows (dry-run)
```

## Branch Strategy 🌿

**State-Specific Development:**

```bash
deploy/dc-*    # DC-only changes (only DC builds in CI)
deploy/co-*    # CO-only changes (only CO builds in CI)
```

**Shared Development:**

```bash
feature/*      # Changes for all states (all states build in CI)
main           # Production source for all states
```

**How it works:** `main` contains all code (shared + state-specific). Each state deployment uses only what it needs via configuration and feature flags.

See [docs/development/state-ci.md](docs/development/state-ci.md) for detailed CI documentation.

## State-Specific Configuration

The API loads state-specific configuration based on the `STATE` environment variable:

1. **`appsettings.json`**: Base configuration (always loaded)
2. **`appsettings.{STATE}.json`**: State overrides (loaded when `STATE` is set)

When `STATE` is set, the API looks for `appsettings.{state}.json` in the application directory. Values in the state file override those in `appsettings.json` if present.

**Example:** With `STATE=dc`, the API loads `appsettings.dc.json`. With `STATE=co`, it loads `appsettings.co.json`.

```bash
# Build and run for DC (loads appsettings.dc.json (if present))
STATE=dc dotnet run --project apps/portal/src/SEBT.Portal.Api

# Docker Compose uses STATE from .env
docker compose up
```

Only include sections you want to override; other settings fall back to `appsettings.json`!

### OIDC support

States can use an external [OpenID Connect (OIDC)](https://openid.net/developers/how-connect-works/) provider for sign-in. OIDC is configured in the API under flat `Oidc` keys (`DiscoveryEndpoint`, `ClientId`, `CallbackRedirectUri`); the portal uses generic endpoints and config rather than state-specific auth code paths. Code exchange and id_token validation run in the Next.js server; the .NET API performs "complete-login" (validates a short-lived callback token and returns a portal JWT that includes IdP claims such as phone and name).

For a deployment that uses OIDC, in `.env.local` under `SEBT.Portal.Web`, set:

- `OIDC_DISCOVERY_ENDPOINT`
- `OIDC_CLIENT_ID`
- `OIDC_CLIENT_SECRET`
- `OIDC_REDIRECT_URI`
- `OIDC_COMPLETE_LOGIN_SIGNING_KEY` (at least 32 characters)

In `appsettings` under `SEBT.Portal.Api`, set:

- `Oidc:CompleteLoginSigningKey` (same value as `OIDC_COMPLETE_LOGIN_SIGNING_KEY`)
- `Oidc:DiscoveryEndpoint`
- `Oidc:ClientId`
- `Oidc:CallbackRedirectUri`
- `Oidc:LanguageParam` (optional)

The API serves public config via `GET /api/auth/oidc/{stateCode}/config` (no secrets in that response).

See `apps/portal/src/SEBT.Portal.Api/appsettings.Development.example.json` and [ADR-0008](docs/adr/0008-oidc-mycolorado-authentication-and-state-auth-context.md).

There is a local Keycloak stand-in that can be used for local development if desired. See [docs/development/keycloak-oidc.md](docs/development/keycloak-oidc.md) and `appsettings.keycloak.example.json` for additional details.

### Development Phone Override (Local dev only)

For states that use phone number as their primary Household ID and OIDC, local development sometimes requires bypassing MFA. You can override the phone number used for household lookup in `appsettings.Development.json`.

**Only active when `ASPNETCORE_ENVIRONMENT=Development`.** Example:

```json
"DevelopmentPhoneOverride": {
  "Phone": "8185558437"
}
```

The resolver then uses this phone for household lookup instead of the one from the JWT or user record. You can still complete the OIDC flow as usual; the phone number used to satisfy MFA may differ from the one the portal uses for lookups.

### OTP Bypass (DAST scanning, non-production only)

To let SEBT's DAST (Dynamic Application Security Testing) scanner exercise the email login flow without receiving a one-time password, the portal can bypass OTP validation for a single, well-known scanner identity. The bypass is gated by **all** of the following criteria — if any one fails, normal OTP validation applies:

1. The `bypass_otp` feature flag is enabled (`FeatureManagement` in `appsettings.json`; defaults to `false`).
2. The application is running in a **non-production** environment (`ASPNETCORE_ENVIRONMENT` is anything other than `Production`).
3. The request email matches the scanner-specific address (`OtpBypassSettings.Email`).
4. (Validation only) The submitted OTP matches the fixed scanner code (`OtpBypassSettings.OtpCode`).

**Never enable `bypass_otp` in production, and never use the scanner email for a real user account.** The settings live in [`OtpBypassSettings`](apps/portal/src/SEBT.Portal.Core/AppSettings/OtpBypassSettings.cs); the gating is enforced in [`OtpController`](apps/portal/src/SEBT.Portal.Api/Controllers/Auth/OtpController.cs).

### ID Proofing Requirements

The `IdProofingRequirements` config section controls which IAL (Identity Assurance Level) a user needs to view or modify each type of PII. Keys use a `resource+action` format (e.g. `address+view`, `card+write`). Values can be a uniform level (`"IAL1plus"`) or a per-case-type object for granular control. Unconfigured keys default to `IAL1plus` (fail-safe). Users below the view threshold see masked data (e.g. `****` for street addresses); users below the write threshold are blocked from modifications.

See the [full configuration guide](docs/config/ial/README.md) for all available keys, per-case-type syntax, coherence validation rules, and state-specific examples. See [`appsettings.dc.example.json`](apps/portal/src/SEBT.Portal.Api/appsettings.dc.example.json) and [`appsettings.co.example.json`](apps/portal/src/SEBT.Portal.Api/appsettings.co.example.json) for working state configurations.

## Database Setup

### MSSQL Server

The application uses Microsoft SQL Server as its database. This is propped up via a Docker container for local development.

#### Configuration

Configuration is managed through environment variables.

Available environment variables for `.env` in the respository root:
**Database (for Docker Compose):**

- `MSSQL_SA_PASSWORD` - SQL Server SA password
- `MSSQL_DATABASE` - Database name
- `MSSQL_USER` - Database user
- `MSSQL_SERVER` - Server hostname (for local)
- `MSSQL_PORT` - Server port

**API**

- `JWTSETTINGS__SECRETKEY` - Secret key for JWT token signing. Must be at least 32 characters.
- `IDENTIFIERHASHER__SECRETKEY` - Secret key for HMAC-SHA256 hashing of Household Identifiers as needed. Must be at least 32 characters.

### Database Migrations

The application uses [EF, or Entity Framework Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli) to manage database schema changes.

#### Automatic Migrations

**Migrations run automatically on application startup.** When the API starts, it checks for pending migrations and applies them automatically. This ensures the database schema is always up-to-date.

#### Manual Migration Commands

While migrations run automatically, you can also manage them manually by installing `ef` on your local machine:

**List all migrations:**

```bash
dotnet ef migrations list \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Apply pending migrations:**

```bash
dotnet ef database update \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Create a new migration:**

```bash
dotnet ef migrations add MigrationName \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Remove the last migration (if not applied):**

```bash
dotnet ef migrations remove \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

#### Migration Files

Migrations are stored in `apps/portal/src/SEBT.Portal.Infrastructure/Migrations/`:

- Each migration has a timestamp prefix (e.g., `20251212171249_AddUserOptInTable.cs`)
- The `PortalDbContextModelSnapshot.cs` file tracks the current model state
- Migration files should be committed to version control

### Database Seeding

#### Automatic Seeding

The database is automatically seeded with test users when running in the **Development** environment. Seeding occurs automatically during:

- Database migrations (`dotnet ef database update`)
- Application startup (when migrations are applied)
- `DbContext.EnsureCreated()` calls

The automatic seeding uses EF Core's `UseSeeding` mechanism under the hood. See <https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding>

To help test different workflows and users in different states, the seeder will create the following users unless instructed otherwise:

- `co-loaded@example.com` - A co-loaded user with completed ID proofing
- `non-co-loaded@example.com` - A non-co-loaded user with in-progress ID proofing
- `not-started@example.com` - A user who hasn't started ID proofing

Seeding only runs if no users exist in the database, preventing duplicate data on subsequent runs.

#### Clearing Seeded Data

There's occasionally going to be instances where you'd want have the auto-seeded data be not be created for certain types of testing. For those instances, there's a small console app to help with this.

To clear all seeded data from the database, use the `ClearSeededData` console application:

```bash
dotnet run --project scripts/ClearSeededData
```

This will prompt for confirmation before deleting all seeded records from the database. This is irreversable; once done, you'll have to reseed.

**View database tables example:**

```bash
docker exec -it sebt_mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd -d SebtPortal -C \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
```

Alternatively, I'd highly recommend a tool like [LINQPad](https://www.linqpad.net/) to help with DB-related tasks.

## Documentation 📚

More documentation can be found in the [docs](./docs) folder.

See also:

- [README for SEBT.Portal.Web (front end)](./apps/portal/src/SEBT.Portal.Web/README.md)
- [README for Figma design token scripts](./packages/design-system/design/scripts/README.md)

We use [Lightweight Architecture Decision Records](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
for tracking architectural decisions, using [adr tools](https://github.com/npryce/adr-tools) to
store them in source control. These can be found in the [docs/adr](./docs/adr) folder.
