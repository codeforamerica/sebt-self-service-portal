# Summer EBT (SUN Bucks) Self-Service Portal and Enrollment Checker

[![State CI](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml/badge.svg)](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml)

## About

This product enables parents/guardians of children eligible for [Summer EBT / SUN Bucks](https://www.fns.usda.gov/summer/sunbucks) to view the status of and manage their benefit.

- The **Enrollment Checker** lets guardians quickly confirm if their child is already enrolled in the program or if they need to apply, without having to log in.

- The **Self-Service Portal** lets guardians log in and:
  - Check Summer EBT benefits and card status for each of their enrolled children
  - See the status of any applications that they submitted
  - View or update their mailing address on file
  - Request a replacement Summer EBT card

This product is currently in use by:

- [Colorado](https://cdhs.colorado.gov/summer-ebt)
- [Washington, DC](https://sunbucks.dc.gov/)

### Repository structure

This project contains all of the code for the enrollment checker and self-service portal products, shared state connector code, and the connector code used in the Colorado implementation. The DC connector code is in an external repository, ([`sebt-self-service-portal-dc-connector`](https://github.com/codeforamerica/sebt-self-service-portal-dc-connector)).

```text
apps/
  portal/                 # deployable application
    src/
      SEBT.Portal.Api                 # ASP.NET core entry point (controllers, middleware, plugin loading) - used by Portal and Enrollment checker
      SEBT.Portal.Core                # Domain models, service interfaces, exceptions, settings
      SEBT.Portal.Infrastructure             # DB context and migrations, repositories, service implementations, external integrations
      SEBT.Portal.Infrastructure.Seeding     # Data seeding for development environments
      SEBT.Portal.UseCases                   # Application layer command/query handlers (auth, households)
      SEBT.Portal.Kernel/Kernel.AspNetCore   # Base classes, ASP.NET extensions
      SEBT.Portal.Web                 # Next.js Portal web app (see [README](./src/SEBT.Portal.Web/README.md))
      SEBT.EnrollmentChecker.Web      # Standalone Next.js web app for Enrollment Checker
    test/, SEBT.Portal.sln
  connectors/
    state/                # MEF plugin contract (interfaces), NuGet-packaged for external consumers
    co/                   # Colorado connector implementation
    dc/                   # placeholder README — the DC connector lives in its external repo
packages/                 # shared JS libraries: @sebt/design-system (design tokens, locale generation, content), @sebt/analytics, observability
scripts/                  # repo-wide dev, CI, and git helper scripts
tofu/                     # infrastructure as code scripts (OpenTofu)
SEBT.slnx                 # top-level .NET solution: portal + in-repo connectors
.github/                  # CI/CD Actions, github automations
```

Other configuration files live in the repository root: `pnpm-workspace.yaml`, `package.json`, `nuget.config`, `global.json`, `Directory.Build.props`.

### Technology stack overview

#### Backend

- Language / framework: [C# with .NET 10](https://dotnet.microsoft.com/en-us/languages/csharp)
- Key libraries: [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet), [Serilog](https://serilog.net/), [Managed Extensibility Framework (MEF)](https://learn.microsoft.com/en-us/dotnet/standard/mef/), [EntityFramework (EF) Core](https://learn.microsoft.com/en-us/ef/core/)
- Package manager: [NuGet](https://www.nuget.org/)

#### Frontend

- Language / framework: [NextJS 16](https://nextjs.org/) with TypeScript
- Key libraries: next, react, [i18next](https://www.i18next.com/), react-i18next, tanstack/react-query, zod
- Package manager: [pnpm](https://pnpm.io/)
- Design system: [USWDS](https://designsystem.digital.gov/), with design tokens for each state

#### Infrastructure

- Infrastructure as code with [OpenTofu](https://opentofu.org/) (Terraform). See [tofu](./tofu/)
- Containers for local development: Docker with [docker-compose](https://docs.docker.com/compose/) or [Podman](https://podman.io/docs/installation)

------

## Local dev set up option 1: ⚡️ Quickstart (recommended)

### 1. Install git

Make sure you have installed [Git](https://git-scm.com/install/) on your local machine

### 2. Run set up script

To set up this project for the very first time, create an empty directory, and run the `init-workspace` script in
it:

On Mac/Linux:

```bash
curl -fsSLO https://raw.githubusercontent.com/codeforamerica/sebt-self-service-portal/main/scripts/dev/init-workspace.sh
bash init-workspace.sh
```

On Windows, in PowerShell:

```powershell
iwr -useb https://raw.githubusercontent.com/codeforamerica/sebt-self-service-portal/main/scripts/dev/init-workspace.ps1 -OutFile init-workspace.ps1
.\init-workspace.ps1
```

If you've already cloned this project locally, run `./scripts/dev/init-workspace.sh` from inside the root of the repository.

The script will go through the following steps:

- clone this repository, if not already cloned
- clone the separate DC Connector repository as a sibling, if applicable
- check for and install the needed version of any missing software
- install npm DevDependencies
- build the .NET solution
- install Aspire, an open-source tool for setting up and running the app locally. It includes a dashboard UI for accessing the different parts of the app, as well console logs, errors, and telemetry
- issues a local developer certificate to satisfy TLS for Redis caching

### 3. Start application

When the script finishes, run this command to start the application:

```bash
cd sebt-portal-workspace/sebt-self-service-portal
pnpm aspire:dc      #  to start the DC enrollment checker and portal
# OR
pnpm aspire:co      #  to start the Colorado enrollment checker and portal
```

### 4. Access Aspire dashboard

Aspire controls all of the resources that are needed for building and running each state's implementation locally - managing configuration and secrets, creating and seeding databases, starting relevant containers, building the product, running the API, and running the web apps.

Once Aspire is running, go to the dashboard in your browser at <https://localhost:17273>. From there, you can access the enrollment checker web app, portal web app, portal api, and more.

You're set up!

------

### About the set up script

The script will prompt you to install each of these in your home directory:

| Tool | Version from | Installed to |
| ---- | ------------ | ------------ |
| Node | `.nvmrc` | `~/.local/share/sebt/node` |
| pnpm | `engines.pnpm` in `package.json` | `~/.local/share/sebt` |
| .NET SDK | `global.json` | `~/.dotnet` |
| Aspire CLI | `sdk.version` in `aspire.config.json` | the pnpm global directory |
| Podman | not pinned, so the platform package manager picks | wherever that puts it |

Those directories are not on the PATH of a new terminal, so the script offers to add them
to your shell profile at the end. If you decline, the script still works.

If Docker Desktop is not already installed and running, it will offer to install Podman.

#### Options

`--yes` takes every offer without asking, for an unattended run. `--check-only` declines
every offer, which is the way to see what a machine is missing. `--ssh` clones the repo using SSH.

Behind a firewall that inspects TLS, add `--system-certs`. That one flag is the whole
answer: git, Node, pnpm, curl, and .NET each read a different trust store, and the script
points all of them at the one your machine already has. Reach for `--ca-bundle <file>`
only when the proxy root is a file that was never added to that store. Run the script with
`--help` for the rest.

The DC connector is a private repository. If you cannot access it, the script will skip installing it.

### Using Aspire for local development

- Aspire does not require you to create local `.env` and `appsettings` files. The AppHost provides every value that the API needs at startup. If you have local `appsettings` files, the values from the AppHost win, because an environment variable has a higher priority than a JSON file
- Aspire selects the host ports for each run, so the URL you use to access the portal and enrollment checker may vary each time you restart
- The API does not have hot reloading, so after you change C# code, run `aspire resource api rebuild`. The portal web and enrollment checker web applications keep hot reload from their own development servers.
- To control just one resource, run `aspire resource <name> stop`, `start`, or `rebuild`. The other running resources will not be affected
- Currently, running the Colorado app with Aspire only supports Keycloak for the login, not MyColorado OIDC. Read the [Keycloak guide](./docs/development/keycloak-oidc.md) for info about users and passwords. To use MyColorado and test with CBMS, please follow the manual install steps below.

Some other helpful CLI commands:

- `pnpm aspire:status` to show the graph and address of each resource
- `pnpm aspire:stop` to stop all running resources

------

## Local dev set up option 2: manual install / legacy process

Below are steps to manually install and set up all of the software and configuration for running the app locally. If you've already got the app running with Aspire, you can skip these steps.

You may need to run these steps if you cannot run the setup script / Aspire, or if you need to utilize functionality that is not yet supported in the Aspire set up.

**Important**:

- These steps are for macOS using [Homebrew package manager](https://brew.sh/). Steps may differ on a different operating system.
- On Windows, make long paths available using `git config core.longpaths true`, since the nested `apps/portal/...` paths can exceed the legacy limit of 260 characters.

### 1. Install prerequisite software

- [Git](https://git-scm.com/install/)
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) for the backend, see `global.json` for the version
  - To install it with Homebrew, run `brew install dotnet`
- [nvm](https://github.com/nvm-sh/nvm) for nodeJS version management
  - `brew install nvm`
- [nodeJS](https://nodejs.org/en), see `.nvmrc` for the version
  - `brew install node`
- [pnpm](https://pnpm.io/installation/) 10 or later for frontend package management and
  development scripts. `engines.pnpm` in `package.json` contains the minimum version.
  - `brew install pnpm`, or `npm install -g pnpm@10`
- A container runtime for local development: either [Podman](https://podman.io/docs/installation) or [Docker Desktop](https://www.docker.com/)

### 2. Clone the repository

```bash
git clone git@github.com:codeforamerica/sebt-self-service-portal.git
```

#### To run the DC implementation

The state connector for DC has its own repository. See [apps/connectors/dc/README.md](./apps/connectors/dc/README.md). Clone it into the same parent folder as this repository on your local machine, so it can be used when building and running the DC app.

```bash
git clone git@github.com:codeforamerica/sebt-self-service-portal-dc-connector.git
```

### 3. Configure your local environment

This project uses **`.env` files** to set environment variables (for example, local database configurations). This is a preferred pattern for [12-factor Apps](https://www.12factor.net/config). The variables are also set to fall back to a generic default.

To create your local `.env` file with configurations for the database and the API, you can start with the `.env.example` file. Run this command from the root of the repository:

```bash
cp .env.example .env
```

Do the same in `apps/portal/src/SEBT.Portal.Web` and `apps/portal/src/SEBT.EnrollmentChecker.Web`:

```bash
cp .env.example .env.local
```

You should also create the API **`appsettings.json` files** in your local environment with certain values set, based on the example files:

```bash
cd apps/portal/src/SEBT.Portal.Api

cp appsettings.Development.example.json appsettings.Development.json
cp appsettings.co.example.json appsettings.co.json   # for colorado local development
cp appsettings.dc.example.json appsettings.dc.json   # for DC local development
```

For more details about how appsettings work, see [state-specific configuration](#state-specific-configuration) below.

### 4. Install dependencies

#### Frontend

- To install all JavaScript package dependencies, run `pnpm install` from the root of this repository.
- For more details about the frontend, see the [SEBT.Portal.Web README](./apps/portal/src/SEBT.Portal.Web/README.md).

#### Backend

- .NET tools are CLI utilities installed and managed using [NuGet](https://www.nuget.org/). This project uses the [`nuget-license`](https://www.nuget.org/packages/nuget-license) tool to audit the licenses of the backend dependencies. The manifest in `.config/dotnet-tools.json` defines the necessary tools. To install them, run `dotnet tool restore` at the root of the repository.
- Before you start the app locally for the first time, run `dotnet build SEBT.slnx` from the root of the repository. This command builds the portal and the in-repo connectors together.

### 5. Start services

Make sure that Docker is installed and the Docker daemon is running. Several components of the app are containerized for local development.

#### Start the database in Docker

Before you start the app, start the container for the MSSQL database with `docker compose up -d mssql`. When the database starts locally, it runs all migrations and seeds test data automatically (see [database setup](#database-setup) section below).

#### Start Mailpit in Docker (DC Portal only)

[Mailpit](https://mailpit.axllent.org/) is a development tool that captures all outgoing emails from the application. It's used to simulate one-time-password (OTP) authentication in local development.

To start the Mailpit Docker container, run `docker compose up -d mailpit`. Once the container is running, access its UI in your browser at <http://localhost:8025>.

#### Other services for local dev in Docker

- Redis (caching). See below
- Jaeger (telemetry). See below

### 6. Build and run the app

Once all dependencies are installed and running, you can use these start commands:

```bash
`pnpm dev:dc` # to start the DC Portal (using the external `dc-connector` repo alongside this repo)
`pnpm dev:dc-enroll` # to start the DC Enrollment Checker (using the external `dc-connector` repo alongside this repo)
`pnpm dev:co`  #  to start the CO Portal
`pnpm dev:co-enroll` # to start the CO Enrollment Checker
```

To view the running app, go to <https://localhost:3000> in your browser.

### Local Redis (distributed cache)

[Redis](https://redis.io/) is an optional distributed cache for `HybridCache`, included in Docker Compose. It is set to run with TLS enabled, to mirror AWS Elasticache in-transit encryption.

Before you run Redis the first time, use this script to generate the local TLS certificates:

```bash
./scripts/dev/gen-redis-certs.sh
```

The script writes self-signed certificates to `certs/` (gitignored). It is idempotent and does not write over existing certificates. If Redis TLS stops, run the script again. The certificates expire after one year.

Then run `docker compose up -d redis`.

#### Ports

| Port | Protocol | Used by |
|------|----------|---------|
| 6379 | plain    | `redis-commander`, direct `redis-cli` |
| 6380 | TLS      | portal API |

#### Configuration

To connect to the TLS port, add the following to your local `appsettings.{state}.json` file:

```json
"Redis": {
  "Host": "localhost",
  "Port": 6380,
  "Ssl": true,
  "SslHost": "redis",
  "AcceptSelfSignedCertificates": true
}
```

`SslHost` should match the hostname in the server certificate. Locally, this is `redis` (the Docker service name). In production, this is the Elasticache cluster endpoint. The optional `Password` field supports Redis AUTH tokens.

`AcceptSelfSignedCertificates: true` bypasses CA trust for the local self-signed certificate. **Never set this value in production.** Elasticache presents an AWS-signed cert that .NET trusts natively. For the full example, see `appsettings.co.example.json`.

The legacy `ConnectionStrings:Redis` connection string is still permitted as a fallback, but new deployments should use the structured form.

If you don't configure either of these, the application falls back to in-memory caching only. For a full example, see `appsettings.co.example.json`.

### Jaeger (local OpenTelemetry tracing)

[Jaeger](https://github.com/jaegertracing/jaeger) is a local OTLP collector for OpenTelemetry tracing. The default configuration for this application sends traces and metrics via OTLP over gRPC to <http://localhost:4317>, which is the standard port. To view tracing locally, open the Jaeger UI at [http://localhost:16686](http://localhost:16686).

The Next.js web apps (`SEBT.Portal.Web` and `SEBT.EnrollmentChecker.Web`) also emit OTLP here, but only if `OTEL_EXPORTER_OTLP_ENDPOINT` is set. If the variable is not set, the apps send no data. In `.env.example`, the variable points to `http://localhost:4317`. To see web-tier traces alongside the API's, copy that value into your `.env.local` file.

### State-specific configuration

The API loads the state-specific configuration based on the `STATE` environment variable:

1. **`appsettings.json`**: Base configuration (always loaded)
2. **`appsettings.{STATE}.json`**: State overrides (loaded when `STATE` is set)

When `STATE` is set, the API looks for `appsettings.{state}.json` in the application directory. The values in the state file override the values in `appsettings.json`.

**Example:** With `STATE=dc`, the API loads `appsettings.dc.json`. With `STATE=co`, the API loads `appsettings.co.json`.

```bash
# Build and run for DC (loads appsettings.dc.json if present)
STATE=dc dotnet run --project apps/portal/src/SEBT.Portal.Api

# Docker Compose uses STATE from .env
docker compose up
```

Include only the sections that you want to override. The other settings come from `appsettings.json`.

### OIDC support

States can use an external [OpenID Connect (OIDC)](https://openid.net/developers/how-connect-works/) provider for sign-in. OIDC is configured in the API under flat `Oidc` keys: `DiscoveryEndpoint`, `ClientId`, and `CallbackRedirectUri`. The portal uses generic endpoints and configuration, rather than state-specific auth code paths. Code exchange and id_token validation run in the Next.js server. The .NET API does the "complete-login" step. It validates a short-lived callback token and returns a portal JWT that includes IdP claims, e.g., phone number and name.

For a deployment that uses OIDC, set these values in `.env.local` under `SEBT.Portal.Web`:

- `OIDC_DISCOVERY_ENDPOINT`
- `OIDC_CLIENT_ID`
- `OIDC_CLIENT_SECRET`
- `OIDC_REDIRECT_URI`
- `OIDC_COMPLETE_LOGIN_SIGNING_KEY` (minimum of 32 characters)

Set these values in the appsettings files under `SEBT.Portal.Api`:

- `Oidc:CompleteLoginSigningKey` (the same value as `OIDC_COMPLETE_LOGIN_SIGNING_KEY`)
- `Oidc:DiscoveryEndpoint`
- `Oidc:ClientId`
- `Oidc:CallbackRedirectUri`
- `Oidc:LanguageParam` (optional)

The API serves public configuration at `GET /api/auth/oidc/{stateCode}/config`. That response has no secrets.

See `apps/portal/src/SEBT.Portal.Api/appsettings.Development.example.json` and [ADR-0008](docs/adr/0008-oidc-mycolorado-authentication-and-state-auth-context.md).

A local Keycloak stand-in is available for local development. For more details, see [docs/development/keycloak-oidc.md](docs/development/keycloak-oidc.md) and `appsettings.keycloak.example.json`. You can also deploy a shared Keycloak IdP in AWS for non-production use. See [docs/development/keycloak-preview.md](docs/development/keycloak-preview.md).

### Development phone override (local development only)

For states that use phone number as the primary household identifier for OIDC auth, local development sometimes requires bypassing MFA. You can override the phone number for the household lookup in `appsettings.Development.json`.

**This override is active only when `ASPNETCORE_ENVIRONMENT=Development`.** Example:

```json
"DevelopmentPhoneOverride": {
  "Phone": "8185558437"
}
```

The resolver then uses this phone number for household lookup instead of the phone number from the JWT or user record. You can still complete the OIDC flow as usual. The phone number for MFA can be different from the phone number that the portal uses for household lookup.

### OTP bypass (DAST scanning, non-production only)

To enable DAST (Dynamic Application Security Testing), the scanner must use the email login flow without a one-time password. The portal can bypass OTP validation for a single, well-known scanner identity. The bypass is gated by **all** of the following criteria. If one condition is false, normal OTP validation applies:

1. The `bypass_otp` feature flag is enabled. See `FeatureManagement` in `appsettings.json`. The default value is `false`.
2. The application is running in a **non-production** environment. `ASPNETCORE_ENVIRONMENT` is not `Production`.
3. The request email matches the scanner-specific address in `OtpBypassSettings.Email`.
4. For validation only: the submitted OTP is the same as the fixed scanner code in `OtpBypassSettings.OtpCode`.

**Never enable `bypass_otp` in production. Never use the scanner email for a real user account.** The settings are in [`OtpBypassSettings`](apps/portal/src/SEBT.Portal.Core/AppSettings/OtpBypassSettings.cs). [`OtpController`](apps/portal/src/SEBT.Portal.Api/Controllers/Auth/OtpController.cs) applies the gating conditions.

### ID proofing requirements

The `IdProofingRequirements` configuration section sets the IAL (Identity Assurance Level) that a user needs to view or modify each type of PII within the portal. The keys use a `resource+action` format, for example `address+view` and `card+write`.  Values can be one level for all cases, for example `"IAL1plus"`, or a per-case-type object for granular control. Non-configured keys default to `IAL1plus`, which is the fail-safe value. A user below the `view` threshold sees masked PII data (e.g. `****` for street addresses). A user below the `write` threshold cannot make updates to their address or request a card.

For all available keys, the syntax for each case type, coherence validation rules, and state-specific examples, see the [full configuration guide](docs/config/ial/README.md). For working state configurations, see [`appsettings.dc.example.json`](apps/portal/src/SEBT.Portal.Api/appsettings.dc.example.json) and [`appsettings.co.example.json`](apps/portal/src/SEBT.Portal.Api/appsettings.co.example.json).

### Database setup

#### MSSQL Server DB

The application uses Microsoft SQL Server as its database. This is containerized using Docker for local development environments.

#### Configuration

Configuration is managed through environment variables.

Environment variables are available in the `.env` file at the root of the repository.

**Database (for Docker Compose):**

- `MSSQL_SA_PASSWORD` -  SQL Server System Administrator password
- `MSSQL_DATABASE` -  database name
- `MSSQL_USER` -  database user
- `MSSQL_SERVER` -  server hostname for local use
- `MSSQL_PORT` -  server port

**API**

- `JWTSETTINGS__SECRETKEY` - secret key that signs the JWT token. It must have a minimum of 32 characters.
- `IDENTIFIERHASHER__SECRETKEY` - secret key for HMAC-SHA256 hashing of household identifiers, if configured. It must have a minimum of 32 characters.

#### Database migrations

The application uses [EF, or Entity Framework Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli) to manage changes to the database schema.

#### Automatic migrations

**Migrations run automatically when the application starts.** When the API starts, it looks for migrations that did not run, then it applies them. This ensures the database schema is always up-to-date.

#### Manual migration commands

Though migrations run automatically, you can also manage them manually by installing `ef` on your local machine.

**To list all migrations:**

```bash
dotnet ef migrations list \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**To apply migrations that have not been run:**

```bash
dotnet ef database update \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**To create a new migration:**

```bash
dotnet ef migrations add MigrationName \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**To remove the last migration, if it did not run:**

```bash
dotnet ef migrations remove \
  --project apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

#### Migration files

The migrations are in `apps/portal/src/SEBT.Portal.Infrastructure/Migrations/`:

- Each migration has a timestamp prefix, for example `20251212171249_AddUserOptInTable.cs`
- The `PortalDbContextModelSnapshot.cs` file tracks the current model state
- Migration files should be committed to version control

### Database seeding

#### Automatic seeding

In the **Development** environment, the application seeds test users in the database automatically. This occurs during these operations:

- Running database migrations (`dotnet ef database update`)
- Starting application, when the migrations are applied
- `DbContext.EnsureCreated()` calls

The automatic seeding uses EF Core's  `UseSeeding` mechanism. See <https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding>.

To help test different workflows and users in different states, the seeder creates these users by default:

- `co-loaded@example.com` - a co-loaded user who has completed ID proofing
- `non-co-loaded@example.com` - a non-co-loaded user with ID proofing in progress
- `not-started@example.com` - a user who has not started ID proofing

The seeder only runs if no users exist in the database, to prevent duplicate data on subsequent runs.

#### Development reseed endpoint

`POST /api/dev/seed/reseed/{scenarioName}` restores a single seed persona for mutable full-stack E2E suites. The `Seeding:EnableDevEndpoints` flag controls the endpoint (defaults to **false**). This endpoint is excluded from OpenAPI. Local launch profiles and Docker Compose set the flag to `true`, and CI uses the same launch profile. Do **not** enable the endpoint on a deployed hosts, including public lower environments that still use `ASPNETCORE_ENVIRONMENT=Development`.

#### Clearing seeded data from the database

If you need to remove all of seeded data from your database, e.g., for certain types of testing, you can use the `ClearSeededData` console application:

```bash
dotnet run --project scripts/ClearSeededData
```

This will prompt you to confirm before it deletes all of the seeded records from the database. You cannot undo this operation. Once done, you must seed the data again.

**Example of how to view database tables:**

```bash
docker exec -it sebt_mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd -d SebtPortal -C \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
```

A tool such as [LINQPad](https://www.linqpad.net/) is helpful with database tasks.

-----

## Development

### Useful commands

```bash
# View logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (clears database - do this only if you're OK with dropping your seeded data)
docker compose down -v
```

### Testing

#### Run backend tests

```bash
# from repo root
pnpm api:test         # Run all backend tests
pnpm api:test:unit    # Run backend unit tests only
```

#### Run frontend tests: portal

```bash
# from within SEBT.Portal.Web:
pnpm test            # Run frontend tests (vitest)
pnpm test:e2e        # Run frontend end-to-end (Playwright) tests
pnpm test:a11y       # Run accessibility (pa11y) tests
```

#### Run frontend tests: enrollment checker

```bash
# from within SEBT.EnrollmentChecker.Web:
pnpm test            # Run frontend tests (vitest)
pnpm test:e2e        # Run frontend end-to-end (Playwright) tests
pnpm test:a11y       # Run accessibility (pa11y) tests

```

#### Run tests locally as CI runs them (Release mode)

```bash
# from repo root
pnpm ci:test          # Test frontend + backend
pnpm ci:test:frontend    # Test frontend only
pnpm ci:test:backend     # Test backend only
```

### CI/CD (via GitHub Actions)

- `state-ci.yaml` builds / tests the portal and connectors on all pull requests and pushes. PRs are path-filtered: portal-only changes skip connector-irrelevant jobs and vice versa. A push always runs all of the jobs.
- `deploy-ecr.yaml` builds Docker images and deploys **DC** and **CO** to their development environments. It builds the in-repo state and CO connectors, and it checks out the external DC connector.
- `release-iis-dc.yaml` produces the DC IIS release bundle (validated on every PR).
- `deploy-enrollment-checker.yaml` builds and deploys the static enrollment checker.
- `playwright-e2e.yaml` runs Playwright end-to-end (E2E) tests for each state and Pa11y accessibility checks.
- `build-and-seed-dc-source.yaml` builds the DC seed / source image from the external DC repository.

#### State-based CI tests

```bash
pnpm ci:test:states   # Run the build-and-test job

# Utility commands
pnpm ci:list          # List all ACT workflows
pnpm ci:validate      # Validate workflows (dry-run)
```

### Warnings as errors

The .NET solution is configured with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`.  Any compiler warning makes the build fail.

To let a specific warning code through, change it back to a warning in the applicable `.csproj` file:

```xml
<PropertyGroup>
  <WarningsNotAsErrors>$(WarningsNotAsErrors);CS1591</WarningsNotAsErrors>
</PropertyGroup>
```

Use this property, and not `<NoWarn>`, which silences the warning entirely.

## Branch Strategy

```bash
feature/*      # new product feature or enhancement
chore/*        # maintenance tasks that aren't user-facing - e.g., package or security updates, refactors
fix/*          # bug fixes
main           # production code 
```

See [labeler.yml](.github/labeler.yml) for a complete list of possible branch prefixes.

**How it works:** `main` contains both shared code and state-specific code. Each state deployment uses only the code that it needs, via configuration and feature flags.

For the full CI documentation, see [docs/development/state-ci.md](docs/development/state-ci.md).

## Documentation

The [docs](./docs) folder has more documentation.

See also:

- [README for SEBT.Portal.Web (frontend)](./apps/portal/src/SEBT.Portal.Web/README.md)
- [README for the Figma design token scripts](./packages/design-system/design/scripts/README.md)

This project uses [Lightweight Architecture Decision Records](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) for tracking architectural decisions, using [adr tools](https://github.com/npryce/adr-tools) to keep them in source control. The records are in the [docs/adr](./docs/adr) folder.
