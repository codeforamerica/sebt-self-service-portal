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

## Documentation 📚
More documentation can be found in the [docs](./docs) folder. 

We use [Lightweight Architecture Decision Records](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) 
for tracking architectural decisions, using [adr tools](https://github.com/npryce/adr-tools) to
store them in source control. These can be found in the [docs/adr](./docs/adr) folder.
