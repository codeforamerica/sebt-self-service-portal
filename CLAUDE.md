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
- **Locale JSON files are generated — NEVER hand-edit them.** They are produced by `packages/design-system/content/scripts/generate-locales.js` from CSV exports in `packages/design-system/content/states/`. To add or change content: update the source Google Sheet, re-export the CSV, and re-run the generator (run `pnpm copy:generate` from within `src/SEBT.Portal.Web/` or `src/SEBT.EnrollmentChecker.Web/`). This also runs automatically via the `predev` and `prebuild` hooks. If a key is missing, note it as a content gap to resolve in the spreadsheet — do not add it directly to the JSON.

### Code style
- C#: 4-space indent, Allman brace style (braces on own line), nullable reference types enabled (see `.editorconfig`)
- Frontend: TypeScript (not JavaScript). ESLint + Prettier with organize-imports plugin
- Unix line endings (LF) enforced project-wide

### Frontend styling
- **Avoid inline `style={...}` props.** They bypass the design-token system, can't be themed per state, are hard to override, and don't compose with USWDS modifier classes.
- **Reach for shared design-system components first** (`Button`, `InputField`, `Alert`, … from `@sebt/design-system`) before composing your own from raw HTML + USWDS classes. They already encapsulate ARIA wiring, USWDS class composition, and per-state theming — re-using them keeps behavior consistent and prevents accessibility drift. Use a `<button>` with `usa-button` only when no shared component fits, and consider whether the design system should be extended instead.
- **When no shared component fits, prefer USWDS component classes** (`usa-button`, `usa-input`, `usa-form-group`, `usa-combo-box__list`, …) before writing custom CSS.
- **Use USWDS utility classes** (`position-relative`, `margin-bottom-2`, `text-center`, `display-flex`, …) for layout, spacing, and one-off style needs. The full utility set is generated from our design tokens, so utilities stay in sync with the per-state theme.
- When none of the above fits, add SCSS in a co-located `.scss` file that references USWDS tokens (`@use 'uswds-core' as *;` and the `units()` / `color()` helpers). Don't hardcode colors, spacing, or font sizes.

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
- Architectural changes. Architectural decisions are recorded as ADRs in [docs/adr/](./docs/adr/). Consult existing ADRs before proposing changes that affect architecture. Note: some ADR numbers are accidentally reused (e.g., 0007) and may be cleaned up later — always reference ADRs by filename, not number.
- Security modifications

## Designing Solutions
### 1. Build for composition
- Each service delivers one focused capability.
- When proposing major new functionality, ask: "should this be a separate service?". Be pragmatic, but default to yes if the capability is clearly independently useful. Still, always request confirmation from a human, and if the human declines a separate service, build in a highly modular way that will allow for easy extraction of a composable service later.
### 2. API-first design
- Services expose documented REST APIs.
- This project uses Swashbuckle to auto-generate OpenAPI docs from controller attributes. When adding or changing API endpoints, ensure controller actions have appropriate route, HTTP method, and response type attributes so the generated spec stays accurate.
### 3. Design for deployment
Our government partners require containers that pass security review. Every Dockerfile and `compose.yaml` change must follow these standards:
- Multi-stage builds with slim base images (e.g., `node:24-alpine`). Run as a non-root user.
- Externalize all configuration via environment variables or feature flags. Load secrets from Docker secret files (`/run/secrets`) or environment variables — NEVER hardcode in the image.
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
- Containerization: Docker with Docker Compose for local development

## Testing
We follow a test-driven development (TDD) approach: write tests first to fail, then write the implementation to make them pass.

- **Backend**: xUnit for test framework, NSubstitute for mocking, Bogus for test data generation (see [docs/adr/0007-bogus-factory-pattern-for-test-data.md](./docs/adr/0007-bogus-factory-pattern-for-test-data.md)). Integration tests use Testcontainers with real MSSQL instances.
- **Frontend**: Vitest with React Testing Library for unit tests, Playwright for E2E tests.
- **Pre-commit hook**: The backend pre-commit hook runs `dotnet build` and `dotnet test`. Non-compiling code cannot be committed. For TDD red-phase work, either combine test + implementation in one commit (commit at green), or use `--no-verify` for intermediate commits with a full-hook run before pushing.
- New functionality must include tests. Prefer writing the test before the implementation.
- **Backend test namespace convention**: .NET test namespaces should mirror the implementation namespace. For example, tests for `SEBT.Portal.Infrastructure.Services.JwtTokenService` belong in `SEBT.Portal.Tests.Unit.Infrastructure.Services` (file path: `test/SEBT.Portal.Tests/Unit/Infrastructure/Services/`). Some older tests live in a flat `Unit/Services/` directory — don't follow that pattern. Align incrementally when writing new tests or refactoring existing ones.

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

### Content Security Policy (CSP)
- The portal enforces a strict CSP via `src/SEBT.Portal.Web/src/proxy.ts`. When adding **any browser-side call to a new external domain** (API, SDK, analytics, fonts), add the domain to the appropriate CSP directive (`connect-src`, `script-src`, `style-src`, `font-src`, etc.).
- This is easy to miss because CSP is not enforced in local dev or in tests (MSW intercepts network calls). A missing entry means the feature silently fails in production — the browser blocks the request, and error-handling code gracefully degrades as if the service is down.

### Client-side env vars (`NEXT_PUBLIC_*`)
Next.js inlines `NEXT_PUBLIC_*` references into the client bundle at **build time**, not runtime. Adding a new client-exposed var requires four wire-ups, all needed for it to appear in deployed builds:

1. `src/SEBT.Portal.Web/src/env.ts` — declare in the `client` schema and pass through.
2. `src/SEBT.Portal.Web/Dockerfile` — add `ARG NEXT_PUBLIC_FOO=""` (BuildKit auto-exposes named ARGs as env vars to subsequent RUN steps; no explicit `ENV` bridge needed).
3. `.github/workflows/deploy-ecr.yaml` — add `--build-arg NEXT_PUBLIC_FOO=${{ vars.FOO }}` to **both** the DC and CO docker build steps.
4. **GitHub repo Variables** — set `vars.FOO` per environment (admin, out-of-band).

Skipping any of (2)–(4) results in the var being empty in deployed bundles. Local dev reads from `.env`/`.env.local` and bypasses this whole pipeline, so the gap only surfaces in deployed environments.

### Data boundary enforcement
- Enforce access control at the data boundary (the API endpoint that returns the data), not at the UI layer. Client-side guards are UX conveniences, not security controls.
- When an authenticated user lacks sufficient authorization for a specific resource (e.g., insufficient IAL for their household's cases), return a 403 with structured ProblemDetails — not a 200 with filtered/empty data. The client needs to know *why* access was denied and *what to do about it* (e.g., `requiredIal` in the ProblemDetails extensions).
- Auth claims in JWTs can go stale (e.g., household composition changes after login). Server-side checks that re-evaluate on every request are safer than trusting a token's claims about what the user is allowed to see.

### PII at rest
- Never store PII (household identifiers, case IDs, SSNs) in cleartext in the portal database when the data is only needed for lookups (e.g., cooldown checks, deduplication). Use `IIdentifierHasher` (HMAC-SHA256 with `IdentifierHasher:SecretKey`) to produce deterministic hashes for storage and lookup.
- `IIdentifierHasher.Hash()` normalizes input via `IdentifierNormalizer` (trims whitespace, strips dashes and spaces) before hashing. This means `"SEBT-001"` and `"SEBT001"` produce the same hash. Ensure read and write paths use `Hash()` consistently.
- Hashing is one-way — if there's any uncertainty about whether original cleartext values may need to be retrieved later, stop and ask a human. Encryption or another reversible approach may be more appropriate.

### State connector data boundaries
- State connectors provide read-only household data by default. The portal should not assume connectors support write-back except for well-known write operations: mailing address updates, card replacement requests (yes/no), and contact/communications preferences.
- When the portal needs to enforce business rules (e.g., cooldown periods) based on user actions, persist that state in the portal database rather than relying on state connector round-trips.

## Common Commands
**Prefer pnpm root-level scripts** (`pnpm api:test`, `pnpm api:build`, etc.) over raw `dotnet` commands. They handle working directories correctly and are the team convention. Use raw `dotnet` commands only for targeted operations like single-test filters.

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
pnpm api:test:unit        # Backend unit tests only (excludes Testcontainers/external APIs)
dotnet test --filter "FullyQualifiedName~MyTest"  # Single backend test
cd src/SEBT.Portal.Web && pnpm test              # Frontend tests (Vitest)
cd src/SEBT.Portal.Web && pnpm test:e2e          # Playwright E2E tests
pnpm ci:build             # Full Release build
pnpm ci:test              # Full Release test suite
```

### State-Specific Development
```bash
pnpm dev:dc               # Build DC plugin + start API & Web for DC
pnpm dev:co               # Build CO plugin + start API & Web for CO
pnpm api:build-dc         # Build DC connector plugin only
pnpm api:build-co         # Build CO connector plugin only
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
- **Infrastructure.Seeding** — Development-only data seeding services
- **Kernel** / **Kernel.AspNetCore** — Cross-cutting base classes and ASP.NET extensions
- **Web** — Next.js 16 frontend (React 19, USWDS 3.13, i18next)
- **EnrollmentChecker.Web** — Next.js enrollment-check standalone app (separate pnpm workspace member)
- **TestUtilities** — Shared test helpers (Bogus factories, builders)
- **Tests** / **UseCases.Tests** — xUnit + NSubstitute + Bogus + Testcontainers (MSSQL)

#### Workspace Packages (`packages/`)
- **design-system** — USWDS design tokens, locale generation scripts, shared content
- **analytics** — Analytics instrumentation library

### Layer boundaries
- Inner layers (Kernel, Core, UseCases) must not reference web/HTTP concepts (ProblemDetails, status codes, headers, controllers). They define abstractions; outer layers (Api, Web) decide how to serialize and transport them.

### Domain model: Cases vs Applications
- A `SummerEbtCase` represents a single child with issued benefits. Most cases are auto-issued (no application). Card/EBT data and benefit delivery belong here.
- An `Application` represents a guardian-submitted application for one or more children. Only a small fraction of children have one. A Case may link to an Application, but most won't.
- State backends represent this differently: DC distinguishes cases from applications similarly to the portal; Colorado represents everything as an "application." The state connector mapping layer disaggregates into the portal's canonical model based on state-specific attributes.
- Known tech debt: `Application` still carries card lifecycle fields (`CardStatus`, `CardRequestedAt`, etc.) that belong on `SummerEbtCase`.

### Multi-State Plugin System
State-specific behavior uses MEF (System.Composition) plugins loaded at runtime from `plugins-{state}/` directories. Plugin contracts live in the separate `sebt-self-service-portal-state-connector` repo; implementations live in per-state repos (`-dc-connector`, `-co-connector`). The `STATE` env var controls which state config overlay loads. See [docs/adr/0007-multi-state-plugin-approach.md](./docs/adr/0007-multi-state-plugin-approach.md) for the design rationale.

**Plugin development inner loop:** The state-connector repo builds its interface package to `~/nuget-store/` as a local NuGet source. The API project and state connector repos (e.g., `-dc-connector`) reference that package and have post-build targets that copy compiled DLLs into this repo's `src/SEBT.Portal.Api/plugins-{state}/` directory. After building a connector, restart the API to pick up changes.

**Mock data mode:** When `UseMockHouseholdData` is `true` (set in `appsettings.{state}.json`), the API uses in-memory mock data from `MockHouseholdRepository` instead of calling state connector plugins. This affects both reads and writes. Mock data scenarios are defined in `MockHouseholdRepository.SeedMockData()` with test personas keyed by email/phone.

### Frontend
Uses Next.js App Router with route groups: `(public)/` for login flows, `(authenticated)/` for protected pages. USWDS design tokens are generated via scripts before build. i18next handles internationalization with content files in `content/`.

**React Query cache:** When a mutation should update cached data before navigating, `await queryClient.invalidateQueries()` — without `await`, the redirect races ahead and the destination page renders stale cache data.

## Branch Strategy
- `main` — production source for all states
- `feature/*` — in-progress changes (all states build in CI)

## Pull Requests
All PRs (in this repo and related repos) must follow the template in [.github/pull_request_template.md](./.github/pull_request_template.md). When creating a PR, populate every section of the template: Jira ticket link, description, related PRs, and completion checklist.

## References
- USWDS Design System: https://designsystem.digital.gov
- Docker Docs: https://docs.docker.com
- Docker Compose Docs: https://docs.docker.com/compose
- OpenAPI 3.x Spec: https://spec.openapis.org/oas/latest.html
- OWASP Top Ten web application security risks: https://owasp.org/Top10/2025/ 
