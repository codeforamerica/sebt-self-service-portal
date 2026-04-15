# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose
The Summer EBT (SUN Bucks) Self-Service Portal allows parents/guardians to manage Summer EBT benefits for eligible children. The portal supports multiple states (DC, CO) via a plugin architecture. See [README.md](./README.md) for full background and setup instructions.

## Interaction norms
We're colleagues working together. Neither of us is afraid to admit we don't know something or are in over our head. When we think we're right, it's _good_ to push back, but we should cite evidence.

## Code-Authoring norms
- We prefer simple, clean, maintainable solutions over clever or complex ones, even if the latter are more concise or performant. Readability and maintainability are primary concerns.
- Doing it right is better than doing it fast. You are not in a rush. NEVER skip steps or take shortcuts.
- Stay focused. Fix only what relates to your current task. Notice something else that needs work? Document it separately rather than fixing it now.
- Preserve comments. They're documentation, not clutter.
- Write evergreen code. Describe what code does, not when it was written. (i.e. avoid "newFunction")
- All user-facing strings must go through i18next. Never hardcode display text in components — reference keys via the translation functions.
- **Locale JSON files are generated — NEVER hand-edit them.** They are produced by `packages/design-system/content/scripts/generate-locales.js` from CSV exports in `packages/design-system/content/states/`. To add or change content: update the source Google Sheet, re-export the CSV, and re-run the generator (`pnpm copy:generate`). If a key is missing, note it as a content gap to resolve in the spreadsheet — do not add it directly to the JSON.

### Code style
- C#: 4-space indent, Allman brace style (braces on own line), nullable reference types enabled (see `.editorconfig`)
- Frontend: TypeScript (not JavaScript). ESLint + Prettier with organize-imports plugin
- Unix line endings (LF) enforced project-wide

## Getting help
- If you're confused or having trouble with something, you are strongly encouraged to stop and ask for help. Especially if it's something your human might be better at.

## Decision-Making Framework
### 🟢 Proceed Immediately
- Fix tests, linting errors, type errors
- Implement single functions with clear specs
- Correct typos, formatting, documentation
- Refactor within a single file to improve clarity
- Add missing imports or dependencies

### 🟡 Propose First
- Changes spanning multiple files
- New features or significant functionality
- API or interface changes

### 🔴 Always Explicitly Ask a Human First!
- Rewriting working code from scratch
- Changing core business logic or removing functionality
- Architectural changes. Architectural decisions are recorded as ADRs in [docs/adr/](./docs/adr/). Consult existing ADRs before proposing changes that affect architecture.
- Security modifications

## Designing Solutions
### 1. Build for composition
- Each service delivers one focused capability.
- When proposing major new functionality, ask: "should this be a separate service?". Be pragmatic, but default to yes if the capability is clearly independently useful. Still, always request confirmation from a human, and if the human declines a separate service, build in a highly modular way that will allow for easy extraction of a composable service later.
### 2. API-first design
- Services expose documented REST APIs.
- This project uses Swashbuckle to auto-generate OpenAPI docs from controller attributes. When adding or changing API endpoints, ensure controller actions have appropriate route, HTTP method, and response type attributes so the generated spec stays accurate.
### 3. Design for deployment
- Package services in containers with clear deployment documentation.
- Use docker-compose.yml to define and run the complete application stack and manage services, networking, and dependencies.
- Use multi-stage builds and slim images (like node:24-alpine, etc.) to create small, secure containers.
- Create a non-root user in your Dockerfile and run the container process as that user.
- Docker Compose is to create a reliable, configurable, and secure environment for your multi-container application.
- Externalize all configuration! Configure through environment variables, feature flags, etc.
- Load all secrets from Docker secret files (/run/secrets) or environment variables - NEVER hard code secrets in the image.
### 4. Write for handoff
- Write code assuming the state government partner agency will maintain it without us.
- Include clear README files, architecture decision records, and inline documentation explaining the "why" behind any non-obvious choices.

## Technology Stack
- Backend
  - Language/framework: C# with .NET 10
  - Key architectural libraries: ASP.NET Core, Serilog, System.Composition (MEF), EntityFramework Core
  - Package manager: NuGet
- Frontend
  - Language/framework: NextJS with TypeScript
  - Key architectural libraries: next, react, i18next, react-i18next, tanstack/react-query, zod 
  - Package manager: pnpm
  - Design system: USWDS, with design tokens specified for each state
- Containerization: Docker with docker-compose for local development

## Testing
We follow a test-driven development (TDD) approach: write tests first to fail, then write the implementation to make them pass.

- **Backend**: xUnit for test framework, NSubstitute for mocking, Bogus for test data generation (see ADR-0007 on the factory pattern). Integration tests use Testcontainers with real MSSQL instances.
- **Frontend**: Vitest with React Testing Library for unit tests, Playwright for E2E tests.
- New functionality must include tests. Prefer writing the test before the implementation.

## Dependency Management
- Manage all .NET dependencies with NuGet
- Manage all TypeScript/frontend dependencies with pnpm
- The .NET plugin interfaces are packaged for NuGet to a local filesystem store

## Accessibility (WCAG 2.1 AA)
- USWDS components meet baseline WCAG standards. Existing USWDS primitives should be used wherever possible. But when composing/extending:
    - Provide ARIA labels/roles for interactive elements
    - Ensure keyboard navigation and visible focus states
    - Do not hardcode colors. Leverage USWDS design tokens so that contrast-tested, brand-matching colors are used.

## Security
- NEVER commit secrets or API keys.
- NEVER commit PII — this includes email addresses, even when embedded in file paths (e.g., `/Users/name@org/...`). Use relative paths or repo names in docs, plans, and specs.
- Consider the OWASP Top Ten web application security risks
- Apply CORS and rate-limiting where applicable; return safe error messages.
- In React, avoid 'dangerouslySetInnerHtml'. If rendering HTML, sanitize it first.

### Data boundary enforcement
- Enforce access control at the data boundary (the API endpoint that returns the data), not at the UI layer. Client-side guards are UX conveniences, not security controls.
- When an authenticated user lacks sufficient authorization for a specific resource (e.g., insufficient IAL for their household's cases), return a 403 with structured ProblemDetails — not a 200 with filtered/empty data. The client needs to know *why* access was denied and *what to do about it* (e.g., `requiredIal` in the ProblemDetails extensions).
- Auth claims in JWTs can go stale (e.g., household composition changes after login). Server-side checks that re-evaluate on every request are safer than trusting a token's claims about what the user is allowed to see.

## Common Commands

### Development
```bash
dotnet restore            # Install .NET dependencies for solution/project
pnpm install              # Install all NPM dependencies
docker compose up -d      # Start MSSQL and Mailpit
pnpm dev                  # Start API + Web concurrently
```

### Build & Test
```bash
pnpm api:build            # Backend (Debug)
pnpm api:test             # All backend tests
dotnet test --filter "FullyQualifiedName~MyTest"  # Single backend test
cd src/SEBT.Portal.Web && pnpm test              # Frontend tests (Vitest)
cd src/SEBT.Portal.Web && pnpm test:e2e          # Playwright E2E tests
pnpm ci:build             # Full Release build
pnpm ci:test              # Full Release test suite
```

### Database Migrations (EF Core)
Migrations auto-apply on startup. For manual operations, both flags are always required:
```bash
dotnet ef migrations add MigrationName \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

### Linting
```bash
cd src/SEBT.Portal.Web && pnpm lint   # ESLint
cd src/SEBT.Portal.Web && pnpm knip   # Dead code detection
```

## Architecture Overview
This is a .NET 10 + Next.js 16 application following Clean Architecture. For detailed architectural decisions, see [docs/adr/0002-adopt-clean-architecture.md](./docs/adr/0002-adopt-clean-architecture.md).

### Solution Layers
- **Api** — ASP.NET Core entry point, controllers, middleware, plugin loading
- **UseCases** — Application layer: command/query handlers for auth, households
- **Core** — Domain models, service interfaces, exceptions, settings
- **Infrastructure** — EF Core DbContext, repositories, service implementations, migrations
- **Kernel** / **Kernel.AspNetCore** — Cross-cutting base classes and ASP.NET extensions
- **Web** — Next.js 16 frontend (React 19, USWDS 3.13, i18next)
- **Tests** — xUnit + NSubstitute + Bogus + Testcontainers (MSSQL)

### Layer boundaries
- Inner layers (Kernel, Core, UseCases) must not reference web/HTTP concepts (ProblemDetails, status codes, headers, controllers). They define abstractions; outer layers (Api, Web) decide how to serialize and transport them.

### Multi-State Plugin System
State-specific behavior uses MEF (System.Composition) plugins loaded at runtime from `plugins-{state}/` directories. Plugin contracts live in the separate `sebt-self-service-portal-state-connector` repo; implementations live in per-state repos (`-dc-connector`, `-co-connector`). The `STATE` env var controls which state config overlay loads. See ADR-0007 for the design rationale.

**Plugin development inner loop:** The state-connector repo builds its interface package to `~/nuget-store/` as a local NuGet source. The API project and state connector repos (e.g., `-dc-connector`) reference that package and have post-build targets that copy compiled DLLs into this repo's `src/SEBT.Portal.Api/plugins-{state}/` directory. After building a connector, restart the API to pick up changes.

### Frontend
Uses Next.js App Router with route groups: `(public)/` for login flows, `(authenticated)/` for protected pages. USWDS design tokens are generated via scripts before build. i18next handles internationalization with content files in `content/`.

## Branch Strategy
- `main` — production source for all states
- `feature/*` — in-progress changes (all states build in CI)

## References
- USWDS Design System: https://designsystem.digital.gov
- Docker Docs: https://docs.docker.com
- Docker Compose Docs: https://docs.docker.com/compose
- OpenAPI 3.x Spec: https://spec.openapis.org/oas/latest.html
- OWASP Top Ten web application security risks: https://owasp.org/Top10/2025/ 
