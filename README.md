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

## Database Setup

### MSSQL Server

The application uses Microsoft SQL Server as its database.  This is propped up via a Docker container for local development.

#### Configuration

Database configuration is managed through environment variables. Copy `.env.example` to `.env` and customize as needed (this is a preferred pattern for [12-factor Apps](https://www.12factor.net/config)).  They are also set to fallback to a generic default. 

```bash
cp .env.example .env
```

Available environment variables:
- `MSSQL_SA_PASSWORD` - SQL Server SA password
- `MSSQL_DATABASE` - Database name
- `MSSQL_USER` - Database user
- `MSSQL_SERVER` - Server hostname (for local)
- `MSSQL_PORT` - Server port

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
