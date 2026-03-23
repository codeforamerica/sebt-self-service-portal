# Enrollment Checker Web App Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `SEBT.EnrollmentChecker.Web` — a public Next.js app for checking Summer EBT enrollment status, consuming `@sebt/design-system` and supporting SSR (Node container) and SSG (S3/CloudFront) deployment modes.

**Architecture:** Feature-organized Next.js App Router app under `src/SEBT.EnrollmentChecker.Web/`. All pages are client components (SSG-compatible). State managed in `EnrollmentContext` + `sessionStorage`. Browser never calls .NET API directly — always via a Node proxy (this app in SSR mode, or the portal in SSG mode). The portal's existing catch-all `/api/[[...path]]` proxy already handles SSG enrollment API calls — no portal code changes required.

**Tech Stack:** Next.js 16, React 19, TypeScript, `@sebt/design-system`, TanStack Query, Zod 4, i18next, react-hook-form-free (plain controlled inputs + Zod), Vitest + RTL + MSW, Playwright

**Spec:** `docs/superpowers/specs/2026-03-11-enrollment-checker-web-design.md`

**Prerequisite:** Plan 1 (`2026-03-11-design-system-extraction.md`) must be merged to `main` before executing this plan.

> **Commit steps:** Plan documents use standard `git add` / `git commit` syntax. The `commit-commands:commit` skill is an interactive Claude Code shorthand — use it when asked interactively, not in plan documents.

> **API status mapping:** The .NET API returns `Status: "Match" | "NonMatch" | "PossibleMatch" | "Error"`. The frontend maps these: `Match` → enrolled, `NonMatch` → notEnrolled, `Error` → error. `PossibleMatch` is suppressed server-side per spec decision and should never arrive, but if it does, treat as enrolled (same as `Match`).

> **i18n init location:** `import '@/lib/i18n-init'` belongs in `src/providers/Providers.tsx` (a client component), not `layout.tsx`. This ensures i18n is initialized on the client side in both SSR and SSG modes.

---

## File Map

### Created
- `src/SEBT.EnrollmentChecker.Web/package.json` — `@sebt/enrollment-checker` workspace package
- `src/SEBT.EnrollmentChecker.Web/tsconfig.json`
- `src/SEBT.EnrollmentChecker.Web/next.config.ts` — BUILD_STANDALONE + BUILD_STATIC toggle
- `src/SEBT.EnrollmentChecker.Web/vitest.config.ts`
- `src/SEBT.EnrollmentChecker.Web/playwright.config.ts`
- `src/SEBT.EnrollmentChecker.Web/.env.local.example`
- `src/SEBT.EnrollmentChecker.Web/src/app/layout.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/globals.css`
- `src/SEBT.EnrollmentChecker.Web/src/app/styles.scss`
- `src/SEBT.EnrollmentChecker.Web/src/app/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/disclaimer/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/check/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/review/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/results/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/closed/page.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/check/route.ts`
- `src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/schools/route.ts`
- `src/SEBT.EnrollmentChecker.Web/src/lib/env.ts`
- `src/SEBT.EnrollmentChecker.Web/src/lib/stateConfig.ts`
- `src/SEBT.EnrollmentChecker.Web/src/lib/i18n-init.ts`
- `src/SEBT.EnrollmentChecker.Web/src/lib/generated-locale-resources.ts` (auto-generated; placeholder initially)
- `src/SEBT.EnrollmentChecker.Web/src/providers/Providers.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/test-setup.ts`
- `src/SEBT.EnrollmentChecker.Web/src/mocks/handlers.ts`
- `src/SEBT.EnrollmentChecker.Web/src/mocks/server.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.test.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/enrollmentSchema.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/enrollmentSchema.test.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/checkEnrollment.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/checkEnrollment.test.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/getSchools.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/getSchools.test.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks/useSchools.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks/useSchools.test.ts`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ClosedPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ClosedPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/SchoolSelect.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/SchoolSelect.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildResultCard.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildResultCard.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/EnrolledSection.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/EnrolledSection.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/NotEnrolledSection.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/NotEnrolledSection.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ResultsPage.tsx`
- `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ResultsPage.test.tsx`
- `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts`

### Modified
- `pnpm-workspace.yaml` — add `src/SEBT.EnrollmentChecker.Web`

---

## Chunk 1: App Scaffold

### Task 1: Create the enrollment checker Next.js app

**Files:**
- Create: `src/SEBT.EnrollmentChecker.Web/` (entire directory)
- Modify: `pnpm-workspace.yaml`

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/disclaimer
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/check
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/review
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/results
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/closed
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/check
mkdir -p src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/schools
mkdir -p src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components
mkdir -p src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context
mkdir -p src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api
mkdir -p src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks
mkdir -p src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas
mkdir -p src/SEBT.EnrollmentChecker.Web/src/providers
mkdir -p src/SEBT.EnrollmentChecker.Web/src/lib
mkdir -p src/SEBT.EnrollmentChecker.Web/src/mocks
mkdir -p src/SEBT.EnrollmentChecker.Web/e2e
mkdir -p src/SEBT.EnrollmentChecker.Web/public
```

- [ ] **Step 2: Add to pnpm workspace**

Edit `pnpm-workspace.yaml`:

```yaml
packages:
  - 'src/SEBT.Portal.Web'
  - 'src/SEBT.EnrollmentChecker.Web'
  - 'packages/*'
```

- [ ] **Step 3: Create `package.json`**

Create `src/SEBT.EnrollmentChecker.Web/package.json`:

```json
{
  "name": "@sebt/enrollment-checker",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "tokens": "node ../../packages/design-system/design/scripts/generate-tokens.js",
    "tokens:sass": "node ../../packages/design-system/design/scripts/generate-sass-tokens.js",
    "tokens:fonts": "node ../../packages/design-system/design/scripts/generate-fonts.js",
    "copy:generate": "node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app enrollment",
    "postinstall": "sh ../../packages/design-system/design/scripts/copy-uswds-assets.sh",
    "predev": "node ../../packages/design-system/design/scripts/state-banner.js && pnpm --silent tokens && pnpm --silent tokens:sass && pnpm --silent tokens:fonts && pnpm --silent copy:generate",
    "dev": "next dev --turbopack",
    "prebuild": "node ../../packages/design-system/design/scripts/state-banner.js && pnpm --silent tokens && pnpm --silent tokens:sass && pnpm --silent tokens:fonts && pnpm --silent copy:generate",
    "build": "next build",
    "start": "next start",
    "lint": "eslint",
    "pretest": "pnpm --silent copy:generate",
    "test": "vitest",
    "test:e2e": "playwright test",
    "test:a11y": "pa11y-ci"
  },
  "dependencies": {
    "@sebt/design-system": "workspace:*",
    "@t3-oss/env-nextjs": "^0.13.8",
    "@tanstack/react-query": "^5.90.12",
    "@uswds/uswds": "^3.13.0",
    "i18next": "^25.7.3",
    "next": "16.0.8",
    "react": "19.2.1",
    "react-dom": "19.2.1",
    "react-i18next": "^16.5.0",
    "uuid": "^11.0.5",
    "zod": "^4.1.13"
  },
  "devDependencies": {
    "@playwright/test": "^1.57.0",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/react": "^16.3.0",
    "@testing-library/user-event": "^14.6.1",
    "@types/node": "^24",
    "@types/react": "^19",
    "@types/react-dom": "^19",
    "@types/uuid": "^10.0.0",
    "eslint": "^9",
    "eslint-config-next": "16.0.8",
    "jsdom": "^27.3.0",
    "msw": "^2.12.4",
    "pa11y-ci": "^4.0.1",
    "sass": "^1.95.0",
    "sass-embedded": "^1.96.0",
    "sass-loader": "^16.0.6",
    "typescript": "^5",
    "vitest": "^4.0.15"
  }
}
```

- [ ] **Step 4: Create `tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "jsx": "react-jsx",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "allowJs": true,
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "noImplicitReturns": true,
    "exactOptionalPropertyTypes": true,
    "noEmit": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "isolatedModules": true,
    "incremental": true,
    "plugins": [{ "name": "next" }],
    "paths": {
      "@/*": ["./src/*"],
      "@sebt/design-system": ["../../packages/design-system/src/index.ts"],
      "@sebt/design-system/*": ["../../packages/design-system/*"]
    }
  },
  "include": [
    "next-env.d.ts",
    "**/*.ts",
    "**/*.tsx",
    ".next/types/**/*.ts"
  ],
  "exclude": ["node_modules", ".next", "out", "dist", "e2e"]
}
```

- [ ] **Step 5: Create `next.config.ts`**

```typescript
import type { NextConfig } from 'next'
import path from 'path'

const state = process.env.STATE ?? 'co'

// pnpm hoists workspace packages to repo root node_modules.
// __dirname is src/SEBT.EnrollmentChecker.Web/
const designSystemPath = path.resolve(__dirname, '../../node_modules/@sebt/design-system')

const nextConfig: NextConfig = {
  reactCompiler: true,
  transpilePackages: ['@sebt/design-system'],
  env: {
    NEXT_PUBLIC_STATE: state
  },
  experimental: {
    turbopackUseBuiltinSass: false
  },
  sassOptions: {
    implementation: 'sass-embedded',
    includePaths: [
      path.join(designSystemPath, 'design/sass'),
      path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
      path.join(__dirname, 'node_modules')
    ]
  },
  turbopack: {
    rules: {
      '*.scss': {
        loaders: [{
          loader: 'sass-loader',
          options: {
            implementation: 'sass-embedded',
            sassOptions: {
              loadPaths: [
                path.join(designSystemPath, 'design/sass'),
                path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
                path.join(__dirname, 'node_modules')
              ]
            }
          }
        }],
        as: '*.css'
      }
    }
  },
  // Standalone output for Docker/SSR deployments
  ...(process.env.BUILD_STANDALONE === 'true' && { output: 'standalone' as const }),
  // Static export for S3/CloudFront SSG deployments
  ...(process.env.BUILD_STATIC === 'true' && { output: 'export' as const }),
  poweredByHeader: false,
  reactStrictMode: true
}

export default nextConfig
```

- [ ] **Step 6: Create `vitest.config.ts`**

```typescript
import path from 'path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    globals: true,
    css: true,
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**', '.next/**'],
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@sebt/design-system': path.resolve(__dirname, '../../packages/design-system/src/index.ts')
    }
  }
})
```

- [ ] **Step 7: Create `playwright.config.ts`**

```typescript
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry'
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3001',
    reuseExistingServer: !process.env.CI,
    env: {
      PORT: '3001',
      NEXT_PUBLIC_STATE: 'co',
      NEXT_PUBLIC_PORTAL_URL: 'http://localhost:3000',
      NEXT_PUBLIC_APPLICATION_URL: 'http://localhost:3000/apply',
      SKIP_ENV_VALIDATION: 'true'
    }
  }
})
```

- [ ] **Step 8: Create `.env.local.example`**

```bash
# State to build for (dc | co)
NEXT_PUBLIC_STATE=co

# SSR only — .NET API base URL (server-side, never exposed to browser)
BACKEND_URL=http://localhost:5280

# SSG only — portal Node server URL for API proxying
# Leave empty for SSR (same-origin /api routes are used)
NEXT_PUBLIC_API_BASE_URL=

# Feature flags
NEXT_PUBLIC_SHOW_SCHOOL_FIELD=false
NEXT_PUBLIC_CHECKER_ENABLED=true
NEXT_PUBLIC_BOT_PROTECTION_ENABLED=false

# External links (required)
NEXT_PUBLIC_PORTAL_URL=https://portal.example.gov
NEXT_PUBLIC_APPLICATION_URL=https://portal.example.gov/apply

# Analytics (optional)
NEXT_PUBLIC_GA_ID=
```

- [ ] **Step 9: Create `src/lib/env.ts`**

```typescript
import { createEnv } from '@t3-oss/env-nextjs'
import { z } from 'zod'

export const env = createEnv({
  server: {
    NODE_ENV: z.enum(['development', 'test', 'production']).optional(),
    BACKEND_URL: z.string().url().default('http://localhost:5280')
  },
  client: {
    NEXT_PUBLIC_STATE: z.enum(['dc', 'co']),
    NEXT_PUBLIC_API_BASE_URL: z.string().url().optional(),
    NEXT_PUBLIC_SHOW_SCHOOL_FIELD: z.coerce.boolean().default(false),
    NEXT_PUBLIC_CHECKER_ENABLED: z.coerce.boolean().default(true),
    NEXT_PUBLIC_BOT_PROTECTION_ENABLED: z.coerce.boolean().default(false),
    NEXT_PUBLIC_PORTAL_URL: z.string().url(),
    NEXT_PUBLIC_APPLICATION_URL: z.string().url(),
    NEXT_PUBLIC_GA_ID: z.string().regex(/^G-/).optional()
  },
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    BACKEND_URL: process.env.BACKEND_URL,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE,
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
    NEXT_PUBLIC_SHOW_SCHOOL_FIELD: process.env.NEXT_PUBLIC_SHOW_SCHOOL_FIELD,
    NEXT_PUBLIC_CHECKER_ENABLED: process.env.NEXT_PUBLIC_CHECKER_ENABLED,
    NEXT_PUBLIC_BOT_PROTECTION_ENABLED: process.env.NEXT_PUBLIC_BOT_PROTECTION_ENABLED,
    NEXT_PUBLIC_PORTAL_URL: process.env.NEXT_PUBLIC_PORTAL_URL,
    NEXT_PUBLIC_APPLICATION_URL: process.env.NEXT_PUBLIC_APPLICATION_URL,
    NEXT_PUBLIC_GA_ID: process.env.NEXT_PUBLIC_GA_ID
  },
  skipValidation: !!process.env.SKIP_ENV_VALIDATION,
  emptyStringAsUndefined: true
})
```

- [ ] **Step 10: Create `src/lib/stateConfig.ts`**

```typescript
import { env } from './env'

export interface EnrollmentStateConfig {
  state: 'dc' | 'co'
  showSchoolField: boolean
  checkerEnabled: boolean
  botProtectionEnabled: boolean
  portalUrl: string
  applicationUrl: string
  /** SSG: portal Node server URL. SSR: '' (same-origin /api routes). */
  apiBaseUrl: string
}

export function getEnrollmentConfig(): EnrollmentStateConfig {
  return {
    state: env.NEXT_PUBLIC_STATE,
    showSchoolField: env.NEXT_PUBLIC_SHOW_SCHOOL_FIELD,
    checkerEnabled: env.NEXT_PUBLIC_CHECKER_ENABLED,
    botProtectionEnabled: env.NEXT_PUBLIC_BOT_PROTECTION_ENABLED,
    portalUrl: env.NEXT_PUBLIC_PORTAL_URL,
    applicationUrl: env.NEXT_PUBLIC_APPLICATION_URL,
    apiBaseUrl: env.NEXT_PUBLIC_API_BASE_URL ?? ''
  }
}
```

- [ ] **Step 11: Create `src/lib/i18n-init.ts`**

This file is imported by `Providers.tsx` (a client component) to initialize i18next in the browser. It is also imported by `test-setup.ts` for tests.

```typescript
import { initI18n } from '@sebt/design-system'
import { namespaces, stateResources } from './generated-locale-resources'

const state = (process.env.NEXT_PUBLIC_STATE ?? process.env.STATE ?? 'co').toLowerCase()
initI18n(stateResources as Parameters<typeof initI18n>[0], namespaces, state)
```

Note: `generated-locale-resources.ts` is auto-generated by `pnpm copy:generate`. Run it before the first dev/build/test cycle (Step 13). Do not manually edit the generated file.

- [ ] **Step 12: Create `src/app/styles.scss`**

```scss
// ==========================================================================
// SEBT Enrollment Checker Main Stylesheet
// ==========================================================================
// Imports USWDS with state-specific theme from @sebt/design-system.
// uswds-bundle resolves via sassOptions.includePaths.
// ==========================================================================

@forward 'uswds-bundle';
```

Create `src/app/globals.css`:

```css
/* App-level overrides beyond USWDS defaults.
   Add enrollment-checker-specific layout rules here. */
```

- [ ] **Step 13: Create placeholder `src/lib/generated-locale-resources.ts`**

This file will be regenerated by `pnpm copy:generate`. Create a minimal placeholder so the app type-checks before the first generation:

```typescript
// AUTO-GENERATED — do not edit. Run `pnpm copy:generate` to regenerate.
export const namespaces: readonly string[] = []
export const stateResources: Record<string, Record<string, Record<string, unknown>>> = {}
```

- [ ] **Step 14: Create `src/test-setup.ts`**

```typescript
import '@testing-library/jest-dom'
import { afterAll, afterEach, beforeAll } from 'vitest'

// Initialize i18n before tests (mirrors Providers.tsx in production)
import '@/lib/i18n-init'
import { server } from './mocks/server'

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
```

- [ ] **Step 15: Create minimal `src/mocks/server.ts`**

```typescript
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
```

Create placeholder `src/mocks/handlers.ts` (full handlers added in Chunk 2):

```typescript
import type { RequestHandler } from 'msw'

// Handlers are populated in Chunk 2 after schemas are defined.
export const handlers: RequestHandler[] = []
```

- [ ] **Step 16: Create `src/providers/Providers.tsx`**

```typescript
'use client'

// i18n must be initialized before any component that uses useTranslation renders.
// This side-effect import runs when the client module loads in the browser.
import '@/lib/i18n-init'

import { I18nProvider } from '@sebt/design-system'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: { retry: 1, staleTime: 60_000 }
    }
  }))

  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        {children}
      </I18nProvider>
    </QueryClientProvider>
  )
}
```

Note: `EnrollmentProvider` is added to this component in Chunk 5, after `EnrollmentContext` is implemented.

- [ ] **Step 17: Create `src/app/layout.tsx`**

```typescript
import { Footer, Header, HelpSection, SkipNav, getState, getStateName } from '@sebt/design-system'
import type { Metadata, Viewport } from 'next'
import './globals.css'
import './styles.scss'
import { Providers } from '../providers/Providers'

const state = getState()
const stateName = getStateName(state)

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5
}

export const metadata: Metadata = {
  title: {
    default: `${stateName} SUN Bucks Enrollment Checker`,
    template: `%s | ${stateName} SUN Bucks`
  },
  description: `Check if your child is already enrolled in Summer EBT (SUN Bucks) in ${stateName}.`,
  robots: { index: false, follow: false }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" data-state={state} className="usa-js-loading">
      <body>
        <Providers>
          <SkipNav />
          <Header state={state} />
          <main id="main-content">{children}</main>
          <HelpSection state={state} />
          <Footer state={state} />
        </Providers>
        <script src="/js/uswds-init.min.js" defer />
      </body>
    </html>
  )
}
```

- [ ] **Step 18: Install dependencies and run locale generation**

```bash
pnpm install
```

Expected: no errors; `node_modules/@sebt/enrollment-checker` symlink created.

```bash
cd src/SEBT.EnrollmentChecker.Web && NEXT_PUBLIC_STATE=co NEXT_PUBLIC_PORTAL_URL=http://localhost:3000 NEXT_PUBLIC_APPLICATION_URL=http://localhost:3000/apply pnpm copy:generate
```

Expected: `src/lib/generated-locale-resources.ts` is regenerated with enrollment namespaces (`landing`, `disclaimer`, `personalInfo`, `confirmInfo`, `result`, `common`).

- [ ] **Step 19: Run TypeScript check**

```bash
cd src/SEBT.EnrollmentChecker.Web && npx tsc --noEmit
```

Expected: no type errors (the placeholder `generated-locale-resources.ts` is now replaced with real content from `copy:generate`).

- [ ] **Step 20: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/ pnpm-workspace.yaml
git commit -m "DC-172: Scaffold SEBT.EnrollmentChecker.Web app with Next.js, env, providers, and USWDS"
```

---

## Chunk 2: Schemas & Data Layer

### Task 2: Zod schemas, EnrollmentContext, API functions, and MSW mocks

**Files:**
- Create: All files in `src/features/enrollment/schemas/`, `context/`, `api/`, `hooks/`
- Modify: `src/mocks/handlers.ts`

- [ ] **Step 1: Write failing tests for `childSchema`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.test.ts`:

```typescript
import { describe, expect, it } from 'vitest'
import { childSchema } from './childSchema'

describe('childSchema', () => {
  const valid = {
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12'
  }

  it('accepts valid child with required fields', () => {
    expect(childSchema.safeParse(valid).success).toBe(true)
  })

  it('accepts optional middleName', () => {
    expect(childSchema.safeParse({ ...valid, middleName: 'Marie' }).success).toBe(true)
  })

  it('rejects empty firstName', () => {
    const result = childSchema.safeParse({ ...valid, firstName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty lastName', () => {
    const result = childSchema.safeParse({ ...valid, lastName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid DOB format', () => {
    const result = childSchema.safeParse({ ...valid, dateOfBirth: '04/12/2015' })
    expect(result.success).toBe(false)
  })

  it('rejects missing DOB', () => {
    const { dateOfBirth: _, ...noDate } = valid
    const result = childSchema.safeParse(noDate)
    expect(result.success).toBe(false)
  })
})
```

- [ ] **Step 2: Run to verify failure**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- childSchema
```

Expected: FAIL — `childSchema` module not found.

- [ ] **Step 3: Implement `childSchema.ts`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/childSchema.ts`:

```typescript
import { z } from 'zod'

export const childSchema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional(),
  lastName: z.string().min(1, 'Last name is required').max(100),
  // ISO date: yyyy-MM-dd
  dateOfBirth: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Date must be YYYY-MM-DD'),
  schoolName: z.string().max(200).optional(),
  schoolCode: z.string().max(50).optional()
})

export type ChildFormValues = z.infer<typeof childSchema>
```

- [ ] **Step 4: Run to verify pass**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- childSchema
```

Expected: PASS.

- [ ] **Step 5: Write failing tests for `enrollmentSchema`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/enrollmentSchema.test.ts`:

```typescript
import { describe, expect, it } from 'vitest'
import { enrollmentCheckResponseSchema, mapApiStatus } from './enrollmentSchema'

describe('enrollmentCheckResponseSchema', () => {
  it('parses a valid Match result', () => {
    const raw = {
      results: [{
        checkId: 'abc',
        firstName: 'Jane',
        lastName: 'Doe',
        dateOfBirth: '2015-04-12',
        status: 'Match'
      }],
      message: null
    }
    const result = enrollmentCheckResponseSchema.safeParse(raw)
    expect(result.success).toBe(true)
  })

  it('parses NonMatch and Error statuses', () => {
    const raw = {
      results: [
        { checkId: '1', firstName: 'A', lastName: 'B', dateOfBirth: '2015-01-01', status: 'NonMatch' },
        { checkId: '2', firstName: 'C', lastName: 'D', dateOfBirth: '2016-01-01', status: 'Error', statusMessage: 'Service unavailable' }
      ]
    }
    expect(enrollmentCheckResponseSchema.safeParse(raw).success).toBe(true)
  })
})

describe('mapApiStatus', () => {
  it('maps Match to enrolled', () => expect(mapApiStatus('Match')).toBe('enrolled'))
  it('maps PossibleMatch to enrolled', () => expect(mapApiStatus('PossibleMatch')).toBe('enrolled'))
  it('maps NonMatch to notEnrolled', () => expect(mapApiStatus('NonMatch')).toBe('notEnrolled'))
  it('maps Error to error', () => expect(mapApiStatus('Error')).toBe('error'))
  it('maps unknown to error', () => expect(mapApiStatus('Unknown')).toBe('error'))
})
```

- [ ] **Step 6: Run to verify failure**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- enrollmentSchema
```

Expected: FAIL.

- [ ] **Step 7: Implement `enrollmentSchema.ts`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/enrollmentSchema.ts`:

```typescript
import { z } from 'zod'

// ── Request ────────────────────────────────────────────────────────────────

export const childCheckApiRequestSchema = z.object({
  firstName: z.string(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  schoolName: z.string().optional(),
  schoolCode: z.string().optional(),
  /** middleName is not a direct field — sent via additionalFields["MiddleName"] */
  additionalFields: z.record(z.string()).optional()
})

export const enrollmentCheckRequestSchema = z.object({
  children: z.array(childCheckApiRequestSchema)
})

export type EnrollmentCheckRequest = z.infer<typeof enrollmentCheckRequestSchema>
export type ChildCheckApiRequest = z.infer<typeof childCheckApiRequestSchema>

// ── Response ───────────────────────────────────────────────────────────────

export const childCheckApiResponseSchema = z.object({
  checkId: z.string(),
  firstName: z.string(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  // API returns: Match | NonMatch | PossibleMatch | Error
  status: z.string(),
  matchConfidence: z.number().optional().nullable(),
  eligibilityType: z.string().optional().nullable(),
  schoolName: z.string().optional().nullable(),
  statusMessage: z.string().optional().nullable()
})

export const enrollmentCheckResponseSchema = z.object({
  results: z.array(childCheckApiResponseSchema),
  message: z.string().optional().nullable()
})

export type EnrollmentCheckResponse = z.infer<typeof enrollmentCheckResponseSchema>
export type ChildCheckApiResponse = z.infer<typeof childCheckApiResponseSchema>

// ── Status mapping ─────────────────────────────────────────────────────────

export type DisplayStatus = 'enrolled' | 'notEnrolled' | 'error'

/**
 * Maps .NET API status strings to frontend display states.
 * PossibleMatch is server-side suppressed per spec, but treated as enrolled if received.
 */
export function mapApiStatus(apiStatus: string): DisplayStatus {
  switch (apiStatus) {
    case 'Match':
    case 'PossibleMatch':
      return 'enrolled'
    case 'NonMatch':
      return 'notEnrolled'
    default:
      return 'error'
  }
}
```

- [ ] **Step 8: Run to verify pass**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- enrollmentSchema
```

Expected: PASS.

- [ ] **Step 9: Write failing tests for `EnrollmentContext`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.test.tsx`:

```typescript
import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { EnrollmentProvider, useEnrollment } from './EnrollmentContext'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <EnrollmentProvider>{children}</EnrollmentProvider>
)

const child = {
  firstName: 'Jane',
  lastName: 'Doe',
  dateOfBirth: '2015-04-12'
}

describe('EnrollmentContext', () => {
  beforeEach(() => sessionStorage.clear())
  afterEach(() => sessionStorage.clear())

  it('starts with no children', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    expect(result.current.state.children).toHaveLength(0)
    expect(result.current.state.editingChildId).toBeNull()
  })

  it('adds a child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    expect(result.current.state.children).toHaveLength(1)
    expect(result.current.state.children[0]?.firstName).toBe('Jane')
  })

  it('generates a unique id for each child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => {
      result.current.addChild(child)
      result.current.addChild({ ...child, firstName: 'John' })
    })
    const ids = result.current.state.children.map(c => c.id)
    expect(new Set(ids).size).toBe(2)
  })

  it('removes a child by id', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    const id = result.current.state.children[0]!.id
    act(() => result.current.removeChild(id))
    expect(result.current.state.children).toHaveLength(0)
  })

  it('edits a child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    const id = result.current.state.children[0]!.id
    act(() => result.current.updateChild(id, { ...child, firstName: 'Janet' }))
    expect(result.current.state.children[0]?.firstName).toBe('Janet')
  })

  it('persists to sessionStorage and restores on mount', () => {
    const { result, unmount } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    unmount()

    const { result: result2 } = renderHook(() => useEnrollment(), { wrapper })
    expect(result2.current.state.children).toHaveLength(1)
    expect(result2.current.state.children[0]?.firstName).toBe('Jane')
  })

  it('clearState removes children and sessionStorage', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    act(() => result.current.clearState())
    expect(result.current.state.children).toHaveLength(0)
    expect(sessionStorage.getItem('enrollmentState')).toBeNull()
  })
})
```

- [ ] **Step 10: Run to verify failure**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- EnrollmentContext
```

Expected: FAIL.

- [ ] **Step 11: Implement `EnrollmentContext.tsx`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/EnrollmentContext.tsx`:

```typescript
'use client'

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { v4 as uuidv4 } from 'uuid'
import type { ChildFormValues } from '../schemas/childSchema'

// ── Types ──────────────────────────────────────────────────────────────────

export interface Child extends ChildFormValues {
  id: string
}

interface EnrollmentState {
  children: Child[]
  editingChildId: string | null
}

interface EnrollmentActions {
  addChild: (values: ChildFormValues) => void
  updateChild: (id: string, values: ChildFormValues) => void
  removeChild: (id: string) => void
  setEditingChildId: (id: string | null) => void
  clearState: () => void
}

interface EnrollmentContextValue {
  state: EnrollmentState
  actions: EnrollmentActions
}

// Flatten for ergonomic hook usage
interface UseEnrollmentReturn extends EnrollmentState, EnrollmentActions {}

// ── Context ────────────────────────────────────────────────────────────────

const EnrollmentContext = createContext<EnrollmentContextValue | null>(null)

const STORAGE_KEY = 'enrollmentState'

const initialState: EnrollmentState = {
  children: [],
  editingChildId: null
}

function loadFromStorage(): EnrollmentState {
  if (typeof window === 'undefined') return initialState
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as EnrollmentState) : initialState
  } catch {
    return initialState
  }
}

function saveToStorage(state: EnrollmentState): void {
  if (typeof window === 'undefined') return
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export function EnrollmentProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<EnrollmentState>(initialState)

  // Hydrate from sessionStorage after mount (avoids SSR mismatch)
  useEffect(() => {
    setState(loadFromStorage())
  }, [])

  function update(updater: (prev: EnrollmentState) => EnrollmentState) {
    setState(prev => {
      const next = updater(prev)
      saveToStorage(next)
      return next
    })
  }

  const actions: EnrollmentActions = {
    addChild: (values) => update(s => ({
      ...s,
      children: [...s.children, { id: uuidv4(), ...values }]
    })),
    updateChild: (id, values) => update(s => ({
      ...s,
      children: s.children.map(c => c.id === id ? { id, ...values } : c)
    })),
    removeChild: (id) => update(s => ({
      ...s,
      children: s.children.filter(c => c.id !== id)
    })),
    setEditingChildId: (id) => update(s => ({ ...s, editingChildId: id })),
    clearState: () => {
      if (typeof window !== 'undefined') sessionStorage.removeItem(STORAGE_KEY)
      setState(initialState)
    }
  }

  return (
    <EnrollmentContext.Provider value={{ state, actions }}>
      {children}
    </EnrollmentContext.Provider>
  )
}

export function useEnrollment(): UseEnrollmentReturn {
  const ctx = useContext(EnrollmentContext)
  if (!ctx) throw new Error('useEnrollment must be used within EnrollmentProvider')
  const { state, actions } = ctx
  return { ...state, ...actions }
}
```

- [ ] **Step 12: Run to verify pass**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- EnrollmentContext
```

Expected: PASS.

- [ ] **Step 13: Write failing tests for `checkEnrollment`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/checkEnrollment.test.ts`:

```typescript
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import type { Child } from '../context/EnrollmentContext'
import { checkEnrollment } from './checkEnrollment'

const children: Child[] = [
  { id: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' },
  { id: '2', firstName: 'John', middleName: 'A', lastName: 'Doe', dateOfBirth: '2017-06-01' }
]

describe('checkEnrollment', () => {
  it('sends correct request shape to /api/enrollment/check', async () => {
    let captured: unknown
    server.use(
      http.post('/api/enrollment/check', async ({ request }) => {
        captured = await request.json()
        return HttpResponse.json({
          results: [
            { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' },
            { checkId: '2', firstName: 'John', lastName: 'Doe', dateOfBirth: '2017-06-01', status: 'NonMatch' }
          ]
        })
      })
    )

    await checkEnrollment(children, '')
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const body = captured as any
    expect(body.children).toHaveLength(2)
    expect(body.children[0].firstName).toBe('Jane')
    // middleName sent via additionalFields
    expect(body.children[1].additionalFields?.MiddleName).toBe('A')
  })

  it('returns parsed results', async () => {
    server.use(
      http.post('/api/enrollment/check', () =>
        HttpResponse.json({
          results: [
            { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
          ]
        })
      )
    )
    const result = await checkEnrollment([children[0]!], '')
    expect(result.results[0]?.status).toBe('Match')
  })

  it('throws on 429 rate limit', async () => {
    server.use(
      http.post('/api/enrollment/check', () => new HttpResponse(null, { status: 429 }))
    )
    await expect(checkEnrollment(children, '')).rejects.toThrow('rate')
  })

  it('throws on 503 backend unavailable', async () => {
    server.use(
      http.post('/api/enrollment/check', () => new HttpResponse(null, { status: 503 }))
    )
    await expect(checkEnrollment(children, '')).rejects.toThrow()
  })

  it('uses apiBaseUrl prefix for SSG mode', async () => {
    let url = ''
    server.use(
      http.post('http://portal.example.gov/api/enrollment/check', ({ request }) => {
        url = request.url
        return HttpResponse.json({ results: [] })
      })
    )
    await checkEnrollment(children, 'http://portal.example.gov')
    expect(url).toContain('portal.example.gov')
  })
})
```

- [ ] **Step 14: Run to verify failure**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- checkEnrollment
```

Expected: FAIL.

- [ ] **Step 15: Implement `checkEnrollment.ts`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/checkEnrollment.ts`:

```typescript
import {
  enrollmentCheckResponseSchema,
  type EnrollmentCheckResponse
} from '../schemas/enrollmentSchema'
import type { Child } from '../context/EnrollmentContext'

/**
 * POST /api/enrollment/check
 *
 * @param children - children to check, from EnrollmentContext
 * @param apiBaseUrl - SSG: portal Node server URL (NEXT_PUBLIC_API_BASE_URL).
 *                    SSR: '' (same-origin /api route handles it).
 */
export async function checkEnrollment(
  children: Child[],
  apiBaseUrl: string
): Promise<EnrollmentCheckResponse> {
  const url = `${apiBaseUrl}/api/enrollment/check`

  const body = {
    children: children.map(child => {
      const additionalFields: Record<string, string> = {}
      if (child.middleName) {
        additionalFields['MiddleName'] = child.middleName
      }

      return {
        firstName: child.firstName,
        lastName: child.lastName,
        dateOfBirth: child.dateOfBirth,
        ...(child.schoolName ? { schoolName: child.schoolName } : {}),
        ...(child.schoolCode ? { schoolCode: child.schoolCode } : {}),
        ...(Object.keys(additionalFields).length > 0 ? { additionalFields } : {})
      }
    })
  }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (response.status === 429) {
    throw new Error('rate limit exceeded — please wait before trying again')
  }

  if (!response.ok) {
    throw new Error(`enrollment check failed: ${response.status.toString()}`)
  }

  const data: unknown = await response.json()
  return enrollmentCheckResponseSchema.parse(data)
}
```

- [ ] **Step 16: Run to verify pass**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- checkEnrollment
```

Expected: PASS.

- [ ] **Step 17: Implement `getSchools.ts` and its test**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/getSchools.test.ts`:

```typescript
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { getSchools } from './getSchools'

describe('getSchools', () => {
  it('returns school list', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Adams Elementary', code: 'AES' }])
      )
    )
    const schools = await getSchools('')
    expect(schools[0]?.name).toBe('Adams Elementary')
  })

  it('throws on error', async () => {
    server.use(
      http.get('/api/enrollment/schools', () => new HttpResponse(null, { status: 500 }))
    )
    await expect(getSchools('')).rejects.toThrow()
  })
})
```

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- getSchools
```

Expected: FAIL.

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/getSchools.ts`:

```typescript
export interface School {
  name: string
  code: string
}

export async function getSchools(apiBaseUrl: string): Promise<School[]> {
  const url = `${apiBaseUrl}/api/enrollment/schools`
  const response = await fetch(url)
  if (!response.ok) throw new Error(`getSchools failed: ${response.status.toString()}`)
  return response.json() as Promise<School[]>
}
```

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- getSchools
```

Expected: PASS.

- [ ] **Step 18: Implement `useSchools` hook and its test**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks/useSchools.test.ts`:

```typescript
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { useSchools } from './useSchools'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('useSchools', () => {
  it('returns schools when enabled', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Elm School', code: 'ELM' }])
      )
    )
    const { result } = renderHook(() => useSchools({ enabled: true, apiBaseUrl: '' }), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.[0]?.name).toBe('Elm School')
  })

  it('does not fetch when disabled', () => {
    const { result } = renderHook(() => useSchools({ enabled: false, apiBaseUrl: '' }), { wrapper })
    expect(result.current.isFetching).toBe(false)
  })
})
```

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- useSchools
```

Expected: FAIL.

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks/useSchools.ts`:

```typescript
import { useQuery } from '@tanstack/react-query'
import { getSchools } from '../api/getSchools'

interface UseSchoolsOptions {
  enabled: boolean
  apiBaseUrl: string
}

export function useSchools({ enabled, apiBaseUrl }: UseSchoolsOptions) {
  return useQuery({
    queryKey: ['schools', apiBaseUrl],
    queryFn: () => getSchools(apiBaseUrl),
    enabled,
    staleTime: Infinity  // school list doesn't change within a session
  })
}
```

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- useSchools
```

Expected: PASS.

- [ ] **Step 19: Update `mocks/handlers.ts` with full handlers**

```typescript
import { http, HttpResponse } from 'msw'

export const handlers = [
  http.post('/api/enrollment/check', () =>
    HttpResponse.json({
      results: [
        {
          checkId: 'test-1',
          firstName: 'Jane',
          lastName: 'Doe',
          dateOfBirth: '2015-04-12',
          status: 'Match'
        }
      ]
    })
  ),
  http.get('/api/enrollment/schools', () =>
    HttpResponse.json([
      { name: 'Adams Elementary', code: 'AES' },
      { name: 'Baker Middle School', code: 'BMS' }
    ])
  )
]
```

- [ ] **Step 20: Run all tests**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 21: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/schemas/ src/SEBT.EnrollmentChecker.Web/src/features/enrollment/context/ src/SEBT.EnrollmentChecker.Web/src/features/enrollment/api/ src/SEBT.EnrollmentChecker.Web/src/features/enrollment/hooks/ src/SEBT.EnrollmentChecker.Web/src/mocks/
git commit -m "DC-172: Add enrollment schemas, context, API functions, and MSW mocks"
```

---

## Chunk 3: Simple Feature Components

### Task 3: LandingPage, DisclaimerPage, ClosedPage, result display components

**Files:**
- Create: `LandingPage.tsx/test`, `DisclaimerPage.tsx/test`, `ClosedPage.tsx/test`,
  `ChildReviewCard.tsx/test`, `EnrolledSection.tsx/test`, `NotEnrolledSection.tsx/test`, `ChildResultCard.tsx/test`

All components use `useTranslation()` from `react-i18next` for all displayed strings. No hardcoded text. Navigation uses `next/navigation` (`useRouter`).

- [ ] **Step 1: Write failing test for `LandingPage`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { LandingPage } from './LandingPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

describe('LandingPage', () => {
  it('renders a heading and a continue button', () => {
    render(<LandingPage />)
    // Heading should be present (translation key resolves in test env)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
  })

  it('navigates to /disclaimer on continue click', async () => {
    render(<LandingPage />)
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/disclaimer')
  })
})
```

- [ ] **Step 2: Run to verify failure**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- LandingPage
```

Expected: FAIL.

- [ ] **Step 3: Implement `LandingPage.tsx`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/LandingPage.tsx`:

```typescript
'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className="usa-prose">{t('heading')}</h1>
        <p className="usa-intro">{t('body')}</p>
        <Button onClick={() => router.push('/disclaimer')}>
          {t('cta')}
        </Button>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Run to verify pass**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- LandingPage
```

Expected: PASS.

- [ ] **Step 5: Write and implement `DisclaimerPage`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/DisclaimerPage.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DisclaimerPage } from './DisclaimerPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

describe('DisclaimerPage', () => {
  it('renders heading, body and two buttons', () => {
    render(<DisclaimerPage />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
  })

  it('navigates to / on Back', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('navigates to /check on Continue', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })
})
```

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test -- DisclaimerPage
```

Expected: FAIL. Then implement:

```typescript
'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function DisclaimerPage() {
  const { t } = useTranslation('disclaimer')
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('heading')}</h1>
        <div className="usa-prose">
          <p>{t('body')}</p>
        </div>
        <div className="margin-top-4">
          <Button variant="outline" onClick={() => router.push('/')}>
            {t('back', { ns: 'common' })}
          </Button>
          <Button onClick={() => router.push('/check')}>
            {t('continue', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
```

Run again — expected: PASS.

- [ ] **Step 6: Write and implement `ClosedPage` (stub)**

This page is a stub — the `/closed` CSV content does not yet exist. See spec "Content gap — `/closed` page". For now it renders a minimal message using the `common` namespace.

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ClosedPage.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ClosedPage } from './ClosedPage'

describe('ClosedPage', () => {
  it('renders a heading indicating the checker is unavailable', () => {
    render(<ClosedPage />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })
})
```

```typescript
'use client'

import { useTranslation } from 'react-i18next'

export function ClosedPage() {
  // Keys 'closed.title' and 'closed.body' must be added to the checker CSV
  // once content is finalized. See spec: "Content gap — /closed page"
  const { t } = useTranslation('checker')
  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('closed.title')}</h1>
        <p>{t('closed.body')}</p>
      </div>
    </div>
  )
}
```

Run test — expected: PASS.

- [ ] **Step 7: Write and implement `ChildReviewCard`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildReviewCard.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChildReviewCard } from './ChildReviewCard'

const child = { id: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' }

describe('ChildReviewCard', () => {
  it('displays the child name and DOB', () => {
    render(<ChildReviewCard child={child} onEdit={vi.fn()} onRemove={vi.fn()} />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
    expect(screen.getByText(/2015-04-12/)).toBeInTheDocument()
  })

  it('calls onEdit with child id', async () => {
    const onEdit = vi.fn()
    render(<ChildReviewCard child={child} onEdit={onEdit} onRemove={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: /edit/i }))
    expect(onEdit).toHaveBeenCalledWith('1')
  })

  it('calls onRemove with child id', async () => {
    const onRemove = vi.fn()
    render(<ChildReviewCard child={child} onEdit={vi.fn()} onRemove={onRemove} />)
    await userEvent.click(screen.getByRole('button', { name: /remove/i }))
    expect(onRemove).toHaveBeenCalledWith('1')
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { Button } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'

interface ChildReviewCardProps {
  child: Child
  onEdit: (id: string) => void
  onRemove: (id: string) => void
}

export function ChildReviewCard({ child, onEdit, onRemove }: ChildReviewCardProps) {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="usa-card">
      <div className="usa-card__body">
        <p className="usa-prose">
          <strong>{child.firstName} {child.lastName}</strong>
          {' — '}{child.dateOfBirth}
        </p>
        <div className="usa-button-group">
          <Button variant="unstyled" onClick={() => onEdit(child.id)}>
            {t('editChild', { ns: 'common' })}
          </Button>
          <Button variant="unstyled" onClick={() => onRemove(child.id)}>
            {t('removeChild', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
```

Run — PASS.

- [ ] **Step 8: Write and implement `ChildResultCard`, `EnrolledSection`, `NotEnrolledSection`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildResultCard.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ChildResultCard } from './ChildResultCard'

describe('ChildResultCard', () => {
  it('shows enrolled status', () => {
    render(<ChildResultCard firstName="Jane" lastName="Doe" displayStatus="enrolled" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('shows notEnrolled status', () => {
    render(<ChildResultCard firstName="John" lastName="Smith" displayStatus="notEnrolled" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
  })

  it('shows error status with message', () => {
    render(<ChildResultCard firstName="A" lastName="B" displayStatus="error" errorMessage="Service unavailable" />)
    expect(screen.getByText(/Service unavailable/i)).toBeInTheDocument()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import type { DisplayStatus } from '../schemas/enrollmentSchema'

interface ChildResultCardProps {
  firstName: string
  lastName: string
  displayStatus: DisplayStatus
  errorMessage?: string | null
}

export function ChildResultCard({ firstName, lastName, displayStatus, errorMessage }: ChildResultCardProps) {
  return (
    <div className="usa-card" data-status={displayStatus}>
      <div className="usa-card__body">
        <p><strong>{firstName} {lastName}</strong></p>
        {displayStatus === 'error' && errorMessage && (
          <p className="usa-prose text-error">{errorMessage}</p>
        )}
      </div>
    </div>
  )
}
```

Run — PASS.

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/EnrolledSection.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { EnrolledSection } from './EnrolledSection'

const enrolled: ChildCheckApiResponse[] = [
  { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
]

describe('EnrolledSection', () => {
  it('renders enrolled children', () => {
    render(<EnrolledSection children={enrolled} />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('renders nothing when empty', () => {
    const { container } = render(<EnrolledSection children={[]} />)
    expect(container.firstChild).toBeNull()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

export function EnrolledSection({ children }: { children: ChildCheckApiResponse[] }) {
  const { t } = useTranslation('result')
  if (children.length === 0) return null

  return (
    <section>
      <h2>{t('enrolledHeading')}</h2>
      {children.map(child => (
        <ChildResultCard
          key={child.checkId}
          firstName={child.firstName}
          lastName={child.lastName}
          displayStatus="enrolled"
        />
      ))}
    </section>
  )
}
```

Create `NotEnrolledSection` with similar pattern — includes a link to `applicationUrl`:

```typescript
// NotEnrolledSection.test.tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { NotEnrolledSection } from './NotEnrolledSection'

const notEnrolled: ChildCheckApiResponse[] = [
  { checkId: '2', firstName: 'John', lastName: 'Smith', dateOfBirth: '2016-01-01', status: 'NonMatch' }
]

describe('NotEnrolledSection', () => {
  it('renders not-enrolled children and application link', () => {
    render(<NotEnrolledSection children={notEnrolled} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
    expect(screen.getByRole('link')).toHaveAttribute('href', 'https://apply.example.gov')
  })

  it('renders nothing when empty', () => {
    const { container } = render(<NotEnrolledSection children={[]} applicationUrl="" />)
    expect(container.firstChild).toBeNull()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { TextLink } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

interface NotEnrolledSectionProps {
  children: ChildCheckApiResponse[]
  applicationUrl: string
}

export function NotEnrolledSection({ children, applicationUrl }: NotEnrolledSectionProps) {
  const { t } = useTranslation('result')
  if (children.length === 0) return null

  return (
    <section>
      <h2>{t('notEnrolledHeading')}</h2>
      {children.map(child => (
        <ChildResultCard
          key={child.checkId}
          firstName={child.firstName}
          lastName={child.lastName}
          displayStatus="notEnrolled"
        />
      ))}
      <p className="usa-prose">
        {t('notEnrolledCta')}{' '}
        <TextLink href={applicationUrl}>{t('applyLink')}</TextLink>
      </p>
    </section>
  )
}
```

Run all — expected: PASS.

- [ ] **Step 9: Run all tests to confirm no regressions**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 10: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/
git commit -m "DC-172: Add simple enrollment feature components (Landing, Disclaimer, Closed, result display)"
```

---

## Chunk 4: Complex Components & Results Page

### Task 4: ChildForm, ChildFormPage, ReviewPage, ResultsPage

- [ ] **Step 1: Write and implement `SchoolSelect`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/SchoolSelect.test.tsx`:

```typescript
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { SchoolSelect } from './SchoolSelect'

const qcWrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
)

describe('SchoolSelect', () => {
  it('renders nothing when not enabled', () => {
    const { container } = render(
      <SchoolSelect enabled={false} apiBaseUrl="" value="" onChange={vi.fn()} />,
      { wrapper: qcWrapper }
    )
    expect(container.firstChild).toBeNull()
  })

  it('renders a select when enabled and schools load', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Elm School', code: 'ELM' }])
      )
    )
    render(
      <SchoolSelect enabled={true} apiBaseUrl="" value="" onChange={vi.fn()} />,
      { wrapper: qcWrapper }
    )
    await waitFor(() =>
      expect(screen.getByRole('combobox')).toBeInTheDocument()
    )
    expect(screen.getByText('Elm School')).toBeInTheDocument()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { useTranslation } from 'react-i18next'
import { useSchools } from '../hooks/useSchools'

interface SchoolSelectProps {
  enabled: boolean
  apiBaseUrl: string
  value: string
  onChange: (code: string, name: string) => void
}

export function SchoolSelect({ enabled, apiBaseUrl, value, onChange }: SchoolSelectProps) {
  const { t } = useTranslation('personalInfo')
  const { data: schools, isLoading } = useSchools({ enabled, apiBaseUrl })

  if (!enabled) return null

  return (
    <div className="usa-form-group">
      <label className="usa-label" htmlFor="school-select">
        {t('schoolLabel')}
      </label>
      {isLoading ? (
        <p className="usa-prose">{t('schoolLoading', { ns: 'common' })}</p>
      ) : (
        <select
          id="school-select"
          className="usa-select"
          value={value}
          onChange={e => {
            const school = schools?.find(s => s.code === e.target.value)
            onChange(e.target.value, school?.name ?? '')
          }}
        >
          <option value="">{t('schoolSelectPlaceholder')}</option>
          {schools?.map(school => (
            <option key={school.code} value={school.code}>{school.name}</option>
          ))}
        </select>
      )}
    </div>
  )
}
```

Run — PASS.

- [ ] **Step 2: Write and implement `ChildForm`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildForm.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { ChildForm } from './ChildForm'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('ChildForm', () => {
  it('renders required fields', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/date of birth/i)).toBeInTheDocument()
  })

  it('does not render school field when showSchoolField is false', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })

  it('shows validation error on submit when firstName is empty', async () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/first name is required/i)).toBeInTheDocument()
  })

  it('calls onSubmit with valid values', async () => {
    const onSubmit = vi.fn()
    render(<ChildForm onSubmit={onSubmit} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.type(screen.getByLabelText(/first name/i), 'Jane')
    await userEvent.type(screen.getByLabelText(/last name/i), 'Doe')
    await userEvent.type(screen.getByLabelText(/date of birth/i), '2015-04-12')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' })
    )
  })
})
```

Run — FAIL. Implement `ChildForm.tsx`:

```typescript
'use client'

import { Alert, InputField } from '@sebt/design-system'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ChildFormValues } from '../schemas/childSchema'
import { childSchema } from '../schemas/childSchema'
import type { Child } from '../context/EnrollmentContext'
import { SchoolSelect } from './SchoolSelect'

interface ChildFormProps {
  initialValues?: Child
  onSubmit: (values: ChildFormValues) => void
  onCancel?: () => void
  showSchoolField: boolean
  apiBaseUrl: string
}

export function ChildForm({
  initialValues,
  onSubmit,
  onCancel,
  showSchoolField,
  apiBaseUrl
}: ChildFormProps) {
  const { t } = useTranslation('personalInfo')
  const [values, setValues] = useState<Partial<ChildFormValues>>({
    firstName: initialValues?.firstName ?? '',
    middleName: initialValues?.middleName ?? '',
    lastName: initialValues?.lastName ?? '',
    dateOfBirth: initialValues?.dateOfBirth ?? '',
    schoolName: initialValues?.schoolName,
    schoolCode: initialValues?.schoolCode
  })
  const [errors, setErrors] = useState<Partial<Record<keyof ChildFormValues, string>>>({})

  function set(field: keyof ChildFormValues, value: string) {
    setValues(v => ({ ...v, [field]: value }))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const result = childSchema.safeParse(values)
    if (!result.success) {
      const fieldErrors: Partial<Record<keyof ChildFormValues, string>> = {}
      for (const issue of result.error.issues) {
        const key = issue.path[0] as keyof ChildFormValues
        if (!fieldErrors[key]) fieldErrors[key] = issue.message
      }
      setErrors(fieldErrors)
      return
    }
    setErrors({})
    onSubmit(result.data)
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <InputField
        id="firstName"
        label={t('firstNameLabel')}
        value={values.firstName ?? ''}
        onChange={e => set('firstName', e.target.value)}
        error={errors.firstName}
        isRequired
      />
      <InputField
        id="middleName"
        label={t('middleNameLabel')}
        value={values.middleName ?? ''}
        onChange={e => set('middleName', e.target.value)}
      />
      <InputField
        id="lastName"
        label={t('lastNameLabel')}
        value={values.lastName ?? ''}
        onChange={e => set('lastName', e.target.value)}
        error={errors.lastName}
        isRequired
      />
      <InputField
        id="dateOfBirth"
        label={t('dobLabel')}
        value={values.dateOfBirth ?? ''}
        onChange={e => set('dateOfBirth', e.target.value)}
        error={errors.dateOfBirth}
        isRequired
        hint={t('dobHint')}
      />
      <SchoolSelect
        enabled={showSchoolField}
        apiBaseUrl={apiBaseUrl}
        value={values.schoolCode ?? ''}
        onChange={(code, name) => {
          set('schoolCode', code)
          set('schoolName', name)
        }}
      />
      <div className="usa-button-group margin-top-4">
        {onCancel && (
          <button type="button" className="usa-button usa-button--outline" onClick={onCancel}>
            {t('cancel', { ns: 'common' })}
          </button>
        )}
        <button type="submit" className="usa-button">
          {t('continue', { ns: 'common' })}
        </button>
      </div>
    </form>
  )
}
```

Run — PASS.

- [ ] **Step 3: Write and implement `ChildFormPage`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ChildFormPage.test.tsx`:

```typescript
import { act, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { EnrollmentProvider } from '../context/EnrollmentContext'
import { ChildFormPage } from './ChildFormPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <EnrollmentProvider>{children}</EnrollmentProvider>
)

describe('ChildFormPage', () => {
  it('renders in add mode by default', () => {
    render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('shows back-to-home button when no children yet', () => {
    render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { ChildForm } from './ChildForm'

interface ChildFormPageProps {
  showSchoolField: boolean
  apiBaseUrl: string
}

export function ChildFormPage({ showSchoolField, apiBaseUrl }: ChildFormPageProps) {
  const { t } = useTranslation('personalInfo')
  const router = useRouter()
  const { state, addChild, updateChild, setEditingChildId } = useEnrollment()

  const editingChild = state.editingChildId
    ? state.children.find(c => c.id === state.editingChildId)
    : undefined

  const isEditMode = !!editingChild
  const hasChildren = state.children.length > 0

  function handleSubmit(values: ChildFormValues) {
    if (isEditMode && state.editingChildId) {
      updateChild(state.editingChildId, values)
      setEditingChildId(null)
    } else {
      addChild(values)
    }
    router.push('/review')
  }

  function handleCancel() {
    if (isEditMode) setEditingChildId(null)
    router.push(hasChildren ? '/review' : '/')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={() => router.push(hasChildren ? '/review' : '/')}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{isEditMode ? t('editHeading') : t('heading')}</h1>
        <ChildForm
          initialValues={editingChild}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          showSchoolField={showSchoolField}
          apiBaseUrl={apiBaseUrl}
        />
      </div>
    </div>
  )
}
```

Run — PASS.

- [ ] **Step 4: Write and implement `ReviewPage`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ReviewPage.test.tsx`:

```typescript
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { EnrollmentProvider, useEnrollment } from '../context/EnrollmentContext'
import { ReviewPage } from './ReviewPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

// Helper to pre-populate context with a child
function ReviewPageWithChild({
  onSubmit
}: {
  onSubmit: () => void
}) {
  return (
    <EnrollmentProvider>
      <Seeder />
      <ReviewPage onSubmit={onSubmit} />
    </EnrollmentProvider>
  )
}
function Seeder() {
  const { addChild } = useEnrollment()
  // Add child on mount
  act(() => {
    addChild({ firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' })
  })
  return null
}

describe('ReviewPage', () => {
  it('lists added children', async () => {
    render(<ReviewPageWithChild onSubmit={vi.fn()} />)
    expect(await screen.findByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('calls onSubmit when Submit is clicked', async () => {
    const onSubmit = vi.fn()
    render(<ReviewPageWithChild onSubmit={onSubmit} />)
    await screen.findByText(/Jane Doe/i)
    await userEvent.click(screen.getByRole('button', { name: /submit/i }))
    expect(onSubmit).toHaveBeenCalled()
  })

  it('navigates to /check when Add Another is clicked', async () => {
    render(<ReviewPageWithChild onSubmit={vi.fn()} />)
    await screen.findByText(/Jane Doe/i)
    await userEvent.click(screen.getByRole('button', { name: /add another/i }))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import { ChildReviewCard } from './ChildReviewCard'

interface ReviewPageProps {
  onSubmit: () => void
}

export function ReviewPage({ onSubmit }: ReviewPageProps) {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const { state, removeChild, setEditingChildId } = useEnrollment()

  function handleEdit(id: string) {
    setEditingChildId(id)
    router.push('/check')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={() => router.push('/check')}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{t('heading')}</h1>
        {state.children.map(child => (
          <ChildReviewCard
            key={child.id}
            child={child}
            onEdit={handleEdit}
            onRemove={removeChild}
          />
        ))}
        <div className="usa-button-group margin-top-4">
          <Button variant="outline" onClick={() => router.push('/check')}>
            {t('addAnotherChild')}
          </Button>
          <Button onClick={onSubmit}>
            {t('submit', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
```

Run — PASS.

- [ ] **Step 5: Write and implement `ResultsPage`**

Create `src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/ResultsPage.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ResultsPage } from './ResultsPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

const results: ChildCheckApiResponse[] = [
  { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' },
  { checkId: '2', firstName: 'John', lastName: 'Smith', dateOfBirth: '2016-01-01', status: 'NonMatch' },
  { checkId: '3', firstName: 'Alex', lastName: 'Lee', dateOfBirth: '2014-05-05', status: 'Error', statusMessage: 'Service error' }
]

describe('ResultsPage', () => {
  it('renders a heading', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('shows enrolled child in enrolled section', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('shows not-enrolled child in not-enrolled section', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
  })

  it('shows error child with error message', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Service error/i)).toBeInTheDocument()
  })

  it('navigates to /review on Back click', async () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(mockPush).toHaveBeenCalledWith('/review')
  })

  it('buckets children by status correctly — no mixed sections', () => {
    const onlyEnrolled: ChildCheckApiResponse[] = [
      { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
    ]
    render(<ResultsPage results={onlyEnrolled} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
    expect(screen.queryByText(/John Smith/i)).not.toBeInTheDocument()
  })
})
```

Run — FAIL. Implement:

```typescript
'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { mapApiStatus } from '../schemas/enrollmentSchema'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'
import { EnrolledSection } from './EnrolledSection'
import { NotEnrolledSection } from './NotEnrolledSection'

interface ResultsPageProps {
  results: ChildCheckApiResponse[]
  applicationUrl: string
}

export function ResultsPage({ results, applicationUrl }: ResultsPageProps) {
  const { t } = useTranslation('result')
  const router = useRouter()

  const enrolled = results.filter(r => mapApiStatus(r.status) === 'enrolled')
  const notEnrolled = results.filter(r => mapApiStatus(r.status) === 'notEnrolled')
  const errors = results.filter(r => mapApiStatus(r.status) === 'error')

  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={() => router.push('/review')}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{t('heading')}</h1>
        <EnrolledSection children={enrolled} />
        <NotEnrolledSection children={notEnrolled} applicationUrl={applicationUrl} />
        {errors.length > 0 && (
          <section>
            <h2>{t('errorHeading')}</h2>
            {errors.map(child => (
              <ChildResultCard
                key={child.checkId}
                firstName={child.firstName}
                lastName={child.lastName}
                displayStatus="error"
                errorMessage={child.statusMessage}
              />
            ))}
          </section>
        )}
      </div>
    </div>
  )
}
```

Run — PASS.

- [ ] **Step 6: Run all tests**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/features/enrollment/components/
git commit -m "DC-172: Add complex enrollment components (ChildForm, ReviewPage, ResultsPage)"
```

---

## Chunk 5: App Routes, SSR Proxy & Provider Wiring

### Task 5: Thin page routes, SSR API proxy handlers, and final provider wiring

- [ ] **Step 1: Add `EnrollmentProvider` to `Providers.tsx`**

Update `src/SEBT.EnrollmentChecker.Web/src/providers/Providers.tsx`:

```typescript
'use client'

import '@/lib/i18n-init'

import { EnrollmentProvider } from '@/features/enrollment/context/EnrollmentContext'
import { I18nProvider } from '@sebt/design-system'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: { retry: 1, staleTime: 60_000 }
    }
  }))

  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <EnrollmentProvider>
          {children}
        </EnrollmentProvider>
      </I18nProvider>
    </QueryClientProvider>
  )
}
```

- [ ] **Step 2: Create thin app page routes**

These files are minimal — they delegate entirely to the feature component and handle the checker-enabled guard.

`src/SEBT.EnrollmentChecker.Web/src/app/page.tsx`:
```typescript
import { LandingPage } from '@/features/enrollment/components/LandingPage'
export default function Page() { return <LandingPage /> }
```

`src/SEBT.EnrollmentChecker.Web/src/app/disclaimer/page.tsx`:
```typescript
import { DisclaimerPage } from '@/features/enrollment/components/DisclaimerPage'
export default function Page() { return <DisclaimerPage /> }
```

`src/SEBT.EnrollmentChecker.Web/src/app/check/page.tsx`:
```typescript
// apiBaseUrl comes from NEXT_PUBLIC_API_BASE_URL — intentionally public.
// SSR mode: undefined (ChildFormPage calls relative /api/enrollment/* routes, this app's own proxy).
// SSG mode: portal URL (e.g. https://portal.example.gov) — requests go to the portal's catch-all proxy.
// BACKEND_URL (private) is never exposed to the client.
import { ChildFormPage } from '@/features/enrollment/components/ChildFormPage'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  const { showSchoolField, apiBaseUrl } = getEnrollmentConfig()
  return <ChildFormPage showSchoolField={showSchoolField} apiBaseUrl={apiBaseUrl} />
}
```

`src/SEBT.EnrollmentChecker.Web/src/app/review/page.tsx`:
```typescript
'use client'

import { ReviewPage } from '@/features/enrollment/components/ReviewPage'
import { checkEnrollment } from '@/features/enrollment/api/checkEnrollment'
import { useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { Alert } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const { state } = useEnrollment()
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const config = getEnrollmentConfig()

  async function handleSubmit() {
    setError(null)
    setIsSubmitting(true)
    try {
      const response = await checkEnrollment(state.children, config.apiBaseUrl)
      // Pass results via sessionStorage (avoids URL length limits and keeps data off URL)
      sessionStorage.setItem('enrollmentResults', JSON.stringify(response))
      router.push('/results')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error'
      setError(message.includes('rate') ? t('rateLimitError') : t('submitError'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <>
      {error && <Alert variant="error">{error}</Alert>}
      <ReviewPage onSubmit={handleSubmit} />
    </>
  )
}
```

`src/SEBT.EnrollmentChecker.Web/src/app/results/page.tsx`:
```typescript
'use client'

import { ResultsPage } from '@/features/enrollment/components/ResultsPage'
import { enrollmentCheckResponseSchema } from '@/features/enrollment/schemas/enrollmentSchema'
import { getEnrollmentConfig } from '@/lib/stateConfig'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import type { EnrollmentCheckResponse } from '@/features/enrollment/schemas/enrollmentSchema'

export default function Page() {
  const router = useRouter()
  const config = getEnrollmentConfig()
  const [response, setResponse] = useState<EnrollmentCheckResponse | null>(null)

  useEffect(() => {
    const raw = sessionStorage.getItem('enrollmentResults')
    if (!raw) { router.replace('/'); return }
    try {
      setResponse(enrollmentCheckResponseSchema.parse(JSON.parse(raw)))
    } catch {
      router.replace('/')
    }
  }, [router])

  if (!response) return null

  return <ResultsPage results={response.results} applicationUrl={config.applicationUrl} />
}
```

`src/SEBT.EnrollmentChecker.Web/src/app/closed/page.tsx`:
```typescript
import { ClosedPage } from '@/features/enrollment/components/ClosedPage'
export default function Page() { return <ClosedPage /> }
```

- [ ] **Step 3: Create SSR API proxy routes**

These routes are only bundled in SSR mode (`BUILD_STANDALONE=true`). In SSG mode (`BUILD_STATIC=true`), Next.js static export omits them automatically because API routes are server-side only.

`src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/check/route.ts`:
```typescript
import { env } from '@/lib/env'
import { NextRequest, NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const TIMEOUT_MS = 30_000

export async function POST(request: NextRequest): Promise<NextResponse> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS)

  try {
    const body = await request.text()
    const response = await fetch(`${BACKEND_URL}/api/enrollment/check`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // Forward client IP for rate limiting (NextRequest.ip is not available in App Router)
        'X-Forwarded-For': request.headers.get('x-forwarded-for') ?? ''
      },
      body,
      signal: controller.signal
    })

    const data = await response.text()
    return new NextResponse(data, {
      status: response.status,
      headers: { 'Content-Type': 'application/json' }
    })
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      return NextResponse.json({ error: 'Request timeout' }, { status: 504 })
    }
    if (process.env.NODE_ENV === 'development') {
      console.error('Enrollment check proxy error:', error)
    }
    return NextResponse.json({ error: 'Backend unavailable' }, { status: 502 })
  } finally {
    clearTimeout(timeoutId)
  }
}
```

`src/SEBT.EnrollmentChecker.Web/src/app/api/enrollment/schools/route.ts`:
```typescript
import { env } from '@/lib/env'
import { NextResponse } from 'next/server'

const BACKEND_URL = env.BACKEND_URL
const TIMEOUT_MS = 10_000

export async function GET(): Promise<NextResponse> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS)
  try {
    const response = await fetch(`${BACKEND_URL}/api/enrollment/schools`, {
      signal: controller.signal
    })
    if (!response.ok) return NextResponse.json([], { status: response.status })
    const data = await response.text()
    return new NextResponse(data, {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    })
  } catch {
    // Schools endpoint is optional — return empty list as fallback
    return NextResponse.json([])
  } finally {
    clearTimeout(timeoutId)
  }
}
```

- [ ] **Step 4: Verify portal SSG proxy coverage**

The portal already has `src/SEBT.Portal.Web/src/app/api/[[...path]]/route.ts` — a catch-all that forwards ALL `/api/*` requests to the .NET backend. This means for SSG enrollment checker deployments:

- `POST {NEXT_PUBLIC_API_BASE_URL}/api/enrollment/check` → portal catch-all → .NET API ✅
- `GET {NEXT_PUBLIC_API_BASE_URL}/api/enrollment/schools` → portal catch-all → .NET API ✅

**No portal code changes required.** Confirm by inspecting the portal route file:

```bash
cat src/SEBT.Portal.Web/src/app/api/\\[\\[...path\\]\\]/route.ts
```

Expected: shows a `proxyRequest` function forwarding to `BACKEND_URL`. If the portal has removed or restricted this catch-all since it was last read, add dedicated routes. Otherwise, no action needed.

- [ ] **Step 5: Run TypeScript check on the full app**

```bash
cd src/SEBT.EnrollmentChecker.Web && npx tsc --noEmit
```

Expected: no type errors.

- [ ] **Step 6: Run all tests**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 7: Smoke test — dev server starts**

```bash
cd src/SEBT.EnrollmentChecker.Web && NEXT_PUBLIC_STATE=co NEXT_PUBLIC_PORTAL_URL=http://localhost:3000 NEXT_PUBLIC_APPLICATION_URL=http://localhost:3000/apply SKIP_ENV_VALIDATION=true pnpm dev
```

Expected: server starts on port 3000 (or next available), no errors.

- [ ] **Step 8: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/src/app/ src/SEBT.EnrollmentChecker.Web/src/providers/
git commit -m "DC-172: Add app routes, SSR proxy handlers, and wire EnrollmentProvider"
```

---

## Chunk 6: E2E Setup, Accessibility & Deployment

### Task 6: E2E smoke tests, pa11y, and Dockerfiles

- [ ] **Step 1: Write a basic E2E happy path test**

Create `src/SEBT.EnrollmentChecker.Web/e2e/enrollment.spec.ts`:

```typescript
import { expect, test } from '@playwright/test'

test.describe('Enrollment checker happy path', () => {
  test('navigates from landing to results', async ({ page }) => {
    await page.goto('/')

    // Landing page
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.getByRole('button', { name: /continue/i }).click()

    // Disclaimer page
    await expect(page.url()).toContain('/disclaimer')
    await page.getByRole('button', { name: /continue/i }).click()

    // Check page
    await expect(page.url()).toContain('/check')
    await page.getByLabel(/first name/i).fill('Jane')
    await page.getByLabel(/last name/i).fill('Doe')
    await page.getByLabel(/date of birth/i).fill('2015-04-12')
    await page.getByRole('button', { name: /continue/i }).click()

    // Review page
    await expect(page.url()).toContain('/review')
    await expect(page.getByText(/Jane Doe/i)).toBeVisible()
    await page.getByRole('button', { name: /submit/i }).click()

    // Results page
    await expect(page.url()).toContain('/results')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })

  test('back button returns from disclaimer to landing', async ({ page }) => {
    await page.goto('/disclaimer')
    await page.getByRole('button', { name: /back/i }).click()
    await expect(page.url()).toMatch(/\/$/)
  })

  test('/closed page renders', async ({ page }) => {
    await page.goto('/closed')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })
})
```

Note: E2E tests require the dev server to be running. Playwright config handles this via `webServer`. Run:

```bash
cd src/SEBT.EnrollmentChecker.Web && NEXT_PUBLIC_STATE=co NEXT_PUBLIC_PORTAL_URL=http://localhost:3000 NEXT_PUBLIC_APPLICATION_URL=http://localhost:3000/apply pnpm test:e2e
```

Expected: tests pass (MSW is NOT active in Playwright — the submit will hit `/api/enrollment/check` on the dev server which proxies to the .NET API. For CI, either mock the API route or run the full stack. See note below).

> **E2E API mocking note:** For CI without a running .NET backend, add a Next.js route handler in development mode that returns a mock response, OR configure Playwright to intercept network requests:
> ```typescript
> // In the test, before clicking Submit:
> await page.route('**/api/enrollment/check', route =>
>   route.fulfill({
>     status: 200,
>     body: JSON.stringify({
>       results: [{ checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }]
>     })
>   })
> )
> ```
> Add this interception to the happy-path test if running without a live backend.

- [ ] **Step 2: Create `.pa11yci` config**

Create `src/SEBT.EnrollmentChecker.Web/.pa11yci`:

```json
{
  "defaults": {
    "standard": "WCAG2AA",
    "runners": ["axe"],
    "chromeLaunchConfig": { "args": ["--no-sandbox"] }
  },
  "urls": [
    "http://localhost:3001/",
    "http://localhost:3001/disclaimer",
    "http://localhost:3001/check",
    "http://localhost:3001/closed"
  ]
}
```

- [ ] **Step 3: Create Dockerfile for SSR (Node container)**

Create `src/SEBT.EnrollmentChecker.Web/Dockerfile.ssr`:

```dockerfile
# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM node:24-alpine AS builder
WORKDIR /app

# Copy workspace config and all workspace packages
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY packages/ ./packages/
COPY src/SEBT.EnrollmentChecker.Web/ ./src/SEBT.EnrollmentChecker.Web/

# Install dependencies (workspace-aware)
RUN corepack enable && pnpm install --frozen-lockfile

# Build SSR standalone output
ARG STATE=co
ARG NEXT_PUBLIC_PORTAL_URL
ARG NEXT_PUBLIC_APPLICATION_URL
ENV STATE=$STATE \
    NEXT_PUBLIC_PORTAL_URL=$NEXT_PUBLIC_PORTAL_URL \
    NEXT_PUBLIC_APPLICATION_URL=$NEXT_PUBLIC_APPLICATION_URL
RUN BUILD_STANDALONE=true pnpm --filter @sebt/enrollment-checker build

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM node:24-alpine AS runner
WORKDIR /app

# Non-root user for security
RUN addgroup -S app && adduser -S app -G app

COPY --from=builder /app/src/SEBT.EnrollmentChecker.Web/.next/standalone ./
COPY --from=builder /app/src/SEBT.EnrollmentChecker.Web/.next/static ./src/SEBT.EnrollmentChecker.Web/.next/static
COPY --from=builder /app/src/SEBT.EnrollmentChecker.Web/public ./src/SEBT.EnrollmentChecker.Web/public

USER app
EXPOSE 3000
ENV PORT=3000 HOSTNAME="0.0.0.0"
CMD ["node", "src/SEBT.EnrollmentChecker.Web/server.js"]
```

- [ ] **Step 4: Create Dockerfile for SSG (static export)**

Create `src/SEBT.EnrollmentChecker.Web/Dockerfile.ssg`:

```dockerfile
# ── Stage 1: Build static export ───────────────────────────────────────────
FROM node:24-alpine AS builder
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY packages/ ./packages/
COPY src/SEBT.EnrollmentChecker.Web/ ./src/SEBT.EnrollmentChecker.Web/

RUN corepack enable && pnpm install --frozen-lockfile

# Static export — all env vars baked in at build time
ARG STATE=co
ARG NEXT_PUBLIC_API_BASE_URL
ARG NEXT_PUBLIC_PORTAL_URL
ARG NEXT_PUBLIC_APPLICATION_URL
ENV STATE=$STATE \
    NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_PORTAL_URL=$NEXT_PUBLIC_PORTAL_URL \
    NEXT_PUBLIC_APPLICATION_URL=$NEXT_PUBLIC_APPLICATION_URL

RUN BUILD_STATIC=true pnpm --filter @sebt/enrollment-checker build
# Output is in src/SEBT.EnrollmentChecker.Web/out/
# Upload to S3: aws s3 sync src/SEBT.EnrollmentChecker.Web/out/ s3://your-bucket/
# CloudFront: configure error page 404 → /index.html (200 status) for SPA routing
```

- [ ] **Step 5: Verify SSR build**

```bash
cd src/SEBT.EnrollmentChecker.Web && BUILD_STANDALONE=true NEXT_PUBLIC_STATE=co NEXT_PUBLIC_PORTAL_URL=https://portal.example.gov NEXT_PUBLIC_APPLICATION_URL=https://portal.example.gov/apply pnpm build
```

Expected: `.next/standalone/` produced, no errors.

- [ ] **Step 6: Verify SSG build**

```bash
cd src/SEBT.EnrollmentChecker.Web && BUILD_STATIC=true NEXT_PUBLIC_STATE=co NEXT_PUBLIC_API_BASE_URL=https://portal.example.gov NEXT_PUBLIC_PORTAL_URL=https://portal.example.gov NEXT_PUBLIC_APPLICATION_URL=https://portal.example.gov/apply pnpm build
```

Expected: `out/` directory produced with static HTML files, no errors.

> **SSG constraint check:** If Next.js complains about server-only code in static export mode, look for any `import { headers } from 'next/headers'` or similar server API imports. All enrollment checker pages must be `'use client'` and free of server-only imports. The SSR proxy route handlers in `app/api/enrollment/` are silently omitted by Next.js static export — this is expected and correct.

- [ ] **Step 7: Run full unit test suite**

```bash
cd src/SEBT.EnrollmentChecker.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 8: Run TypeScript check**

```bash
cd src/SEBT.EnrollmentChecker.Web && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 9: Commit**

```bash
git add src/SEBT.EnrollmentChecker.Web/
git commit -m "DC-172: Add E2E test setup, pa11y config, and SSR/SSG Dockerfiles"
```

---

*Plan complete. Open a PR targeting `main` after all chunks pass. Prerequisite: Plan 1 (design-system-extraction) must already be merged.*
