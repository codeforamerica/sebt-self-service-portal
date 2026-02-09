# Summer EBT (SUN Bucks) Self-Service Portal

[![State CI](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml/badge.svg)](https://github.com/codeforamerica/sebt-self-service-portal/actions/workflows/state-ci.yaml)

## Background
The Summer EBT (SUN Bucks) Self-Service Portal is an application that allows parents/guardians
of children eligible for [Summer EBT](https://www.fns.usda.gov/summer/sunbucks) manage their benefit, including the following core features:
- Verifying a child's eligibility
- Verifying when and how the benefit will be received (which EBT card)
- Changing mailing address on file
- Requesting a replacement EBT card

## Quick start 🧰
> **Note:** The following steps assume you are working on macOS. Steps may differ if you are working on a different operating system.

### Prerequisites 👷
- The application backend is built with the .NET 10 SDK, which can be downloaded [here](https://dotnet.microsoft.com/en-us/download).
- Frontend packages and local development scripts are managed with [pnpm](https://pnpm.io/).
- [Docker](https://www.docker.com/) is required for packaging and running containers.

### .NET Tools 🛠️
.NET tools are CLI utilities installed and managed using NuGet. Currently, we are using
the `nuget-license` tool for auditing backend dependency license. To install .NET tools,
run `dotnet tool restore` from the solution root. Needed tools are defined in the tools 
manifest in `.config/dotnet-tools.json`.

### Development 💻
```bash
pnpm install          # Install dependencies
```
***
```bash
pnpm dev              # Start both API and frontend
```

```bash
pnpm web:dev          # Start frontend only
```

### Docker Compose
```bash
# Start all services (MSSQL, Mailpit)
docker compose up -d

# View logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (clears database - do this only if you're OK with dropping your seeded data)
docker compose down -v
```

#### Mailpit (Local Email Testing)
Mailpit captures all outgoing emails in development. Access the web UI at http://localhost:8025

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
STATE=dc dotnet run --project src/SEBT.Portal.Api

# Docker Compose uses STATE from .env
docker compose up
```
Only include sections you want to override; other settings fall back to `appsettings.json`!

### ID Proofing Requirements

PII data is only shown and editable to users who meet the ID proofing requirements configured within "IdProofingRequirements" and their current IAL status (for example, `address+view`, `email+view`, `phone+view`). Configure in `appsettings.json` or override with `appsettings.{state}.json`.

Example (`appsettings.json`):
```json
{
  "IdProofingRequirements": {
    "address+view": "IAL1plus",
    "email+view": "IAL1",
    "phone+view": "IAL1"
  }
}
```

## Database Setup

### MSSQL Server

The application uses Microsoft SQL Server as its database.  This is propped up via a Docker container for local development.

#### Configuration

Database configuration is managed through environment variables. Copy `.env.example` to `.env` and customize as needed (this is a preferred pattern for [12-factor Apps](https://www.12factor.net/config)).  They are also set to fallback to a generic default. 

```bash
cp .env.example .env
```

Available environment variables:

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

The application uses [Entity Framework Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli) to manage database schema changes.

#### Automatic Migrations

**Migrations run automatically on application startup.** When the API starts, it checks for pending migrations and applies them automatically. This ensures the database schema is always up-to-date.

#### Manual Migration Commands

While migrations run automatically, you can also manage them manually:

**List all migrations:**
```bash
dotnet ef migrations list \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Apply pending migrations:**
```bash
dotnet ef database update \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Create a new migration:**
```bash
dotnet ef migrations add MigrationName \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

**Remove the last migration (if not applied):**
```bash
dotnet ef migrations remove \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

#### Migration Files

Migrations are stored in `src/SEBT.Portal.Infrastructure/Migrations/`:
- Each migration has a timestamp prefix (e.g., `20251212171249_AddUserOptInTable.cs`)
- The `PortalDbContextModelSnapshot.cs` file tracks the current model state
- Migration files should be committed to version control

### Database Seeding

#### Automatic Seeding

The database is automatically seeded with test users when running in the **Development** environment. Seeding occurs automatically during:
- Database migrations (`dotnet ef database update`)
- Application startup (when migrations are applied)
- `DbContext.EnsureCreated()` calls

The automatic seeding uses EF Core's `UseSeeding` mechanism under the hood.  See https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding

To help test different workflows and users in different states, the seeder will create the following users unless instructed otherwise:
- `co-loaded@example.com` - A co-loaded user with completed ID proofing
- `non-co-loaded@example.com` - A non-co-loaded user with in-progress ID proofing
- `not-started@example.com` - A user who hasn't started ID proofing

Seeding only runs if no users exist in the database, preventing duplicate data on subsequent runs.

#### Clearing Seeded Data

There's occasionally going to be instances where you'd want have the auto-seeded data be not be created for certain types of testing.  For those instances, there's a small console app to help with this.

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

We use [Lightweight Architecture Decision Records](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
for tracking architectural decisions, using [adr tools](https://github.com/npryce/adr-tools) to
store them in source control. These can be found in the [docs/adr](./docs/adr) folder.
