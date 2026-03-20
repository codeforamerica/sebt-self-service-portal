# Enrollment Checker Web App — Design Spec

**Date:** 2026-03-11
**Branch:** feature/DC-172-enrollment-checker
**Status:** Approved

---

## Overview

A new Next.js application (`SEBT.EnrollmentChecker.Web`) that allows parents and guardians to check whether their children are already enrolled in Summer EBT, without requiring login. The app supports multiple states (CO, DC) via per-state builds and can be deployed either as a Node-based SSR container or as a fully static site to S3.

---

## Background & Requirements

### Core user flow
1. **Landing** — Brief explanation of Summer EBT and the checker tool
2. **Disclaimer** — State-specific notice about data usage and privacy
3. **Child form** — Enter a child's name, date of birth, and optionally school
4. **Review** — Review all added children before submitting; add/edit/remove children
5. **Results** — Per-child enrollment status (enrolled, not enrolled, error) with next steps

### Key constraints
- **No authentication** — fully public, no login required
- **Multi-child** — users add children one at a time, submit as a batch
- **Multi-language** — English and Spanish (Amharic for DC); client-side language switching
- **Multi-state** — one build per state; state config controls branding, copy, feature flags
- **Deployment flexibility** — SSR (Node container) or SSG (static S3/CloudFront) per state
- **Browser never calls .NET API** — always proxied through a Node server

---

## Decision Log

| Question | Decision |
|---|---|
| Same repo or new repo? | Same repo — `src/SEBT.EnrollmentChecker.Web/` alongside portal |
| Shared infrastructure? | Full shared `packages/design-system/` — tokens, USWDS sass, i18n scripts, shared UI components |
| Auth | Fully public. Future invisible bot protection (Cloudflare Turnstile/reCAPTCHA v3) behind feature flag |
| School field | State-configurable via env var. Not confirmed for CO yet |
| API relationship | Enrollment checker calls existing portal `.NET` API via Node proxy |
| PossibleMatch handling | API assumed to never return `PossibleMatch` to frontend (configurable threshold planned) |
| Results navigation | `/results` is not a dead end — users can go back to `/review` and add more children |
| CSV content | Enrollment checker strings already present in existing CSVs under `S1` prefix |
| USWDS theming | Single theme per state, shared across portal and enrollment checker |

---

## Implementation Sequencing

This work spans two workstreams that should be executed in order:

**Workstream 1 — Extract shared design system package**
Extract `packages/design-system/` from the existing portal. Refactor the portal to consume it. This carries regression risk for the portal and should be validated with the full portal test suite before merging.

**Workstream 2 — Build enrollment checker app**
Build `SEBT.EnrollmentChecker.Web` consuming `@sebt/design-system`. This workstream can begin once Workstream 1 has merged to `main`.

Each workstream should be its own PR. Do not attempt them in parallel on shared files.

---

## Section 1: Repository & Workspace Structure

### Monorepo layout

```
sebt-self-service-portal/          ← pnpm workspace root
├── packages/
│   └── design-system/             ← new shared package (@sebt/design-system)
│       ├── content/
│       │   ├── states/
│       │   │   ├── co.csv         ← moved from src/SEBT.Portal.Web/content/states/
│       │   │   └── dc.csv
│       │   ├── locales/           ← generated JSON (gitignored)
│       │   └── scripts/
│       │       └── generate-locales.js   ← gains --app flag for per-app barrel files
│       ├── design/
│       │   ├── states/
│       │   │   ├── co.json        ← Figma design tokens
│       │   │   └── dc.json
│       │   ├── scripts/           ← generate-tokens.js, generate-sass-tokens.js, etc.
│       │   └── sass/              ← single USWDS theme per state
│       │       ├── _uswds-theme-co.scss
│       │       ├── _uswds-theme-dc.scss
│       │       └── uswds-bundle.scss
│       └── src/
│           ├── components/        ← Header, Footer, HelpSection, LanguageSelector,
│           │                         Button, InputField, Alert, TextLink
│           ├── providers/
│           │   └── I18nProvider.tsx
│           └── lib/
│               ├── state.ts       ← StateConfig registry
│               ├── links.ts       ← per-state external links
│               └── i18n.ts        ← shared i18next initializer
│
├── src/
│   ├── SEBT.Portal.Web/           ← refactored to consume @sebt/design-system
│   └── SEBT.EnrollmentChecker.Web/  ← new app (consumes @sebt/design-system)
│
└── pnpm-workspace.yaml
```

### Portal refactor scope (Workstream 1)

The existing portal is refactored to import from `@sebt/design-system`:
- USWDS sass bundle (`@use '@sebt/design-system/sass/uswds-bundle'`)
- Design token generation scripts (run from shared package)
- Content CSVs (moved to shared package; portal's `generate-locales.js` updated)
- Shared UI components (Header, Footer, Button, InputField, Alert, TextLink, etc.)
- `I18nProvider`, `state.ts`, `links.ts`, `i18n.ts`

Portal-specific code stays in the portal: auth features, household features, OIDC, route protection.

### Content pipeline

Enrollment checker strings are already in the existing state CSVs under the `S1` prefix (e.g. `S1 - Disclaimer`, `S1 - Personal Information`, `S1 - Confirm Personal Information`, `S1 - Result`). No CSV restructuring needed.

The `generate-locales.js` script gains an `--app` flag. When generating `generated-locale-resources.ts` for each app, it includes only the relevant namespaces:

- **Portal:** `login`, `dashboard`, `idProofing`, `landing`, `disclaimer`, `personalInfo`, `confirmInfo`, `result`, `common`, …
- **Enrollment checker:** `landing`, `disclaimer`, `personalInfo`, `confirmInfo`, `result`, `common`

Shared `GLOBAL` strings (button labels, footer, help section) are in the `common` namespace and available to both apps.

**Amharic (DC):** The DC CSV includes Amharic translations. When a DC enrollment checker build is eventually enabled, the generation script's per-state language map must include `am` for `dc`. The `--app` flag operates on namespaces; language sets remain per-state as they are today.

**Content gap — `/closed` page:** There are currently no `S1 - Closed` rows in either CSV. Before implementing `ClosedPage.tsx`, content for this page must be added to the CSVs (state-specific messaging about when and why the checker is unavailable, and what the user should do instead).

---

## Section 2: Application Architecture

### Routes

```
/              Landing page ("Get $120 in summer food benefits")
/disclaimer    "What to know before we begin" — state-specific copy
/check         Child info form (add or edit a child)
/review        "Here's the information we have so far" — child list, add/edit/remove
/results       "Here's the information we found" — per-child results + next steps
/closed        Shown when checker is feature-flagged off
```

API routes (SSR only — absent in static export):
```
POST /api/enrollment/check    → proxies to .NET POST /api/enrollment/check
GET  /api/enrollment/schools  → proxies to .NET GET /api/enrollment/schools (stubbed)
```

### Form state

```typescript
interface Child {
  id: string           // client-generated UUID
  firstName: string
  middleName?: string
  lastName: string
  dateOfBirth: string  // ISO date string
  schoolName?: string  // only when NEXT_PUBLIC_SHOW_SCHOOL_FIELD=true
  schoolCode?: string
}

interface EnrollmentState {
  children: Child[]
  editingChildId: string | null  // set when user clicks "Update" from /review
}
```

State lives in `EnrollmentContext` (React context). Persisted to `sessionStorage` so a page refresh doesn't lose progress. Cleared only when the user navigates back to `/` (fresh start).

**`middleName` wire format:** The `.NET` `ChildCheckApiRequest` model does not have a `MiddleName` field. `middleName` is passed via `AdditionalFields["MiddleName"]` when present. The state connector is responsible for reading it from `AdditionalFields`.

### Navigation & Back button

```
/           → /disclaimer
/disclaimer → /check
/check      → /review (if children exist) or / (if first child, user cancels)
/review     → /check
/results    → /review  (not a dead end — can add more children and re-submit)

State cleared: only when user navigates to / fresh
```

### API proxying — SSR vs SSG

**SSR mode:**
```
Browser → POST /api/enrollment/check (enrollment checker Node server)
        → POST {BACKEND_URL}/api/enrollment/check (.NET API, private VNET)
```
`BACKEND_URL` is a server-side env var, never exposed to the browser.

**SSG mode:**
```
Browser → POST {NEXT_PUBLIC_API_BASE_URL}/api/enrollment/check (portal Node server)
        → POST {BACKEND_URL}/api/enrollment/check (.NET API, private VNET)
```
`NEXT_PUBLIC_API_BASE_URL` is baked into the static build at build time, pointing to the portal's Node server. The portal Node server gains two new proxy routes (`/api/enrollment/check` and `/api/enrollment/schools`) to support SSG deployments.

### Deployment mode toggle

Controlled by env vars in `next.config.ts`, matching the existing portal's convention:

| Env var | Value | Next.js output | Use case |
|---|---|---|---|
| _(neither set)_ | — | default | Local development |
| `BUILD_STANDALONE` | `true` | `standalone` | Docker Node container (SSR) |
| `BUILD_STATIC` | `true` | `export` | S3 + CloudFront (SSG) |

`BUILD_STANDALONE` mirrors the portal's existing pattern. `BUILD_STATIC` is new and mutually exclusive with `BUILD_STANDALONE`.

**SSG constraint:** All pages use `'use client'` — there is no server-side data fetching. Next.js static export pre-renders each route to an empty shell at build time; context-dependent content (review list, results) hydrates in the browser. This is the standard SPA pattern for `output: 'export'`.

### State-specific configuration

All config is env-var driven, validated with Zod (T3 env pattern):

```
# Server-side (SSR only)
BACKEND_URL                         # .NET API base URL

# Client-side (baked in at build time)
NEXT_PUBLIC_STATE                   # dc | co
NEXT_PUBLIC_API_BASE_URL            # SSG: portal Node URL; SSR: empty (same-origin /api)
NEXT_PUBLIC_SHOW_SCHOOL_FIELD       # boolean
NEXT_PUBLIC_SCHOOL_SEARCH_ENDPOINT  # URL — required if SHOW_SCHOOL_FIELD=true (out of scope for now)
NEXT_PUBLIC_CHECKER_ENABLED         # boolean; false → redirect to /closed
NEXT_PUBLIC_BOT_PROTECTION_ENABLED  # boolean — out of scope for now; hook point only
NEXT_PUBLIC_PORTAL_URL              # link to the Self-Service Portal
NEXT_PUBLIC_APPLICATION_URL         # link to the Summer EBT application
NEXT_PUBLIC_GA_ID                   # Google Analytics ID — out of scope for now; stubbed in env.ts
```

---

## Section 3: Component Structure

```
src/
├── app/                           ← thin pages, delegate to feature components
│   ├── layout.tsx
│   ├── page.tsx                   → <LandingPage />
│   ├── disclaimer/page.tsx        → <DisclaimerPage />
│   ├── check/page.tsx             → <ChildFormPage />
│   ├── review/page.tsx            → <ReviewPage />
│   ├── results/page.tsx           → <ResultsPage />
│   ├── closed/page.tsx            → <ClosedPage />
│   └── api/enrollment/
│       ├── check/route.ts
│       └── schools/route.ts
│
├── features/enrollment/
│   ├── components/
│   │   ├── LandingPage.tsx
│   │   ├── DisclaimerPage.tsx
│   │   ├── ChildFormPage.tsx      ← wraps ChildForm; handles add vs. edit mode
│   │   ├── ChildForm.tsx          ← name + DOB fields + conditional SchoolSelect
│   │   ├── SchoolSelect.tsx       ← school dropdown (conditional on state config)
│   │   ├── ReviewPage.tsx         ← child list + submit button
│   │   ├── ChildReviewCard.tsx    ← single child row: name, DOB, edit/remove
│   │   ├── ResultsPage.tsx        ← enrolled / not-enrolled / error sections
│   │   ├── ChildResultCard.tsx    ← per-child result display
│   │   ├── EnrolledSection.tsx
│   │   ├── NotEnrolledSection.tsx ← list + application next-steps
│   │   └── ClosedPage.tsx
│   ├── context/
│   │   └── EnrollmentContext.tsx  ← state, actions, sessionStorage persistence
│   ├── api/
│   │   ├── checkEnrollment.ts     ← POST /api/enrollment/check; attaches bot token if enabled
│   │   └── getSchools.ts          ← GET /api/enrollment/schools
│   ├── hooks/
│   │   └── useSchools.ts          ← TanStack Query wrapper for school list
│   └── schemas/
│       ├── childSchema.ts         ← Zod: child form validation
│       └── enrollmentSchema.ts    ← Zod: API request/response shapes
│
├── providers/
│   └── Providers.tsx              ← QueryProvider + EnrollmentProvider + I18nProvider
│
├── lib/
│   ├── env.ts                     ← T3 env validation
│   ├── stateConfig.ts             ← typed EnrollmentStateConfig from env
│   └── generated-locale-resources.ts  ← auto-generated
│
└── mocks/
    ├── handlers.ts                ← MSW: mock check + schools endpoints
    └── server.ts
```

---

## Section 4: Testing Strategy

### Unit / integration (Vitest + React Testing Library + MSW)

- Each page component: renders correct content, handles loading/error states
- `ChildForm`: required field validation, DOB format, school dropdown conditionality
- `EnrollmentContext`: add/edit/remove child, sessionStorage round-trip
- API functions: correct request shape, `middleName` → `AdditionalFields` mapping, rate limit (429) error, 503 error
- Results page: correct bucketing of enrolled / not-enrolled / error children
- i18n: smoke tests for en + es — no missing keys for either language

### E2E (Playwright)

- Happy path: landing → disclaimer → add child → review → submit → results
- Multi-child: add 2 children, edit one, remove one, submit
- Error path: API failure banner, rate limit message, per-child error indicator
- Back button at each step
- Static export smoke: build with `BUILD_STATIC=true`, verify no server-only imports
- Accessibility: pa11y-ci on all pages

---

## Section 5: Deployment

### SSR — Dockerfile

Multi-stage build, `standalone` output, non-root user. Same pattern as the existing portal Dockerfile.

```dockerfile
FROM node:24-alpine AS builder
WORKDIR /app
COPY . .
RUN corepack enable && pnpm install --frozen-lockfile
RUN BUILD_STANDALONE=true STATE=co pnpm --filter @sebt/enrollment-checker build

FROM node:24-alpine AS runner
WORKDIR /app
COPY --from=builder /app/src/SEBT.EnrollmentChecker.Web/.next/standalone ./
RUN addgroup -S app && adduser -S app -G app
USER app
EXPOSE 3000
CMD ["node", "server.js"]
```

### SSG — Static export

Produces a flat `out/` directory, uploaded to S3 and served via CloudFront.

```dockerfile
FROM node:24-alpine AS builder
WORKDIR /app
COPY . .
RUN corepack enable && pnpm install --frozen-lockfile
RUN BUILD_STATIC=true STATE=co \
    NEXT_PUBLIC_API_BASE_URL=https://portal.co.example.gov \
    pnpm --filter @sebt/enrollment-checker build
# out/ directory contains the static site — upload to S3
```

**CloudFront note:** SPA routing requires an error page rule: 404 → `/index.html` with 200 response code.

### Portal additions (for SSG deployments)

Two proxy routes added to the existing portal Next.js app:
- `POST /api/enrollment/check` — proxies to .NET API
- `GET /api/enrollment/schools` — proxies to .NET API (stubbed initially)

---

## Out of Scope (for now)

- Invisible bot protection implementation (Turnstile/reCAPTCHA v3) — hook point designed in, not wired up
- School field data connection — `GET /api/enrollment/schools` stubbed; state connector wires to real data later
- DC-specific deployment config — enrollment checker content for DC exists in the CSV, but DC builds and deployment are not in scope for this iteration
- Analytics / GA integration — `NEXT_PUBLIC_GA_ID` stubbed in `env.ts`, not wired up
- `/closed` page content — CSV entries for this page must be added before implementation
