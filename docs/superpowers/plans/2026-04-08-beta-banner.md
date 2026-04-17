# Beta Banner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a feature-flagged, localized beta banner that displays at the top of every page using the USWDS info alert pattern.

**Architecture:** A `'use client'` `BetaBanner` component wraps the existing `<Alert variant="info">` from the design system. It checks `useFeatureFlag('enable_beta_banner')` and returns `null` when off. Placed in the root layout above `<Header />` so it appears on all pages.

**Tech Stack:** React 19, Next.js 16 (App Router), i18next, USWDS Alert, Microsoft.FeatureManagement

**Spec:** `docs/superpowers/specs/2026-04-07-beta-banner-design.md`

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| NEW | `src/SEBT.Portal.Web/src/components/BetaBanner.tsx` | Feature-flagged banner component |
| NEW | `src/SEBT.Portal.Web/src/components/BetaBanner.test.tsx` | Unit tests |
| EDIT | `src/SEBT.Portal.Web/src/app/layout.tsx` | Render `<BetaBanner />` above `<Header />` |
| EDIT | `src/SEBT.Portal.Api/appsettings.json` | Add `enable_beta_banner: false` default |
| EDIT | `src/SEBT.Portal.Api/appsettings.co.json` | Add `enable_beta_banner: true` opt-in |
| EDIT | `src/SEBT.Portal.Web/src/mocks/handlers.ts` | Add `enable_beta_banner` to `TEST_FEATURE_FLAGS` |

---

### Task 1: Add feature flag to backend config

**Files:**
- Modify: `src/SEBT.Portal.Api/appsettings.json:107-118`
- Modify: `src/SEBT.Portal.Api/appsettings.co.json:42-44`
- Modify: `src/SEBT.Portal.Web/src/mocks/handlers.ts:38-45`

- [ ] **Step 1: Add `enable_beta_banner: false` to default appsettings.json**

In `src/SEBT.Portal.Api/appsettings.json`, add the flag to the `FeatureManagement` section (after `show_card_last4`):

```json
  "FeatureManagement": {
    "email_dob_opt_in": false,
    "show_application_number": true,
    "show_case_number": true,
    "show_card_last4": true,
    "enable_beta_banner": false,
    "AppConfig": {
```

- [ ] **Step 2: Add `enable_beta_banner: true` to CO appsettings**

In `src/SEBT.Portal.Api/appsettings.co.json`, add to the existing `FeatureManagement` section:

```json
  "FeatureManagement": {
    "enable_card_replacement": true,
    "enable_beta_banner": true
  },
```

- [ ] **Step 3: Add `enable_beta_banner` to test mock flags**

In `src/SEBT.Portal.Web/src/mocks/handlers.ts`, add to `TEST_FEATURE_FLAGS`:

```typescript
export const TEST_FEATURE_FLAGS = {
  enable_enrollment_status: true,
  enable_card_replacement: false,
  enable_spanish_support: true,
  show_application_number: true,
  show_case_number: true,
  show_card_last4: true,
  enable_beta_banner: false
} as const
```

Note: default is `false` so existing tests aren't affected.

- [ ] **Step 4: Commit**

```
feat: add enable_beta_banner feature flag (off by default, on for CO)
```

---

### Task 2: Write failing tests for BetaBanner component

**Files:**
- Create: `src/SEBT.Portal.Web/src/components/BetaBanner.test.tsx`

- [ ] **Step 1: Write the test file**

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import { BetaBanner } from './BetaBanner'

function renderWithFlags(overrides: Partial<typeof TEST_FEATURE_FLAGS> = {}) {
  const flags: FeatureFlagsContextValue = {
    flags: { ...TEST_FEATURE_FLAGS, ...overrides },
    isLoading: false,
    isError: false
  }

  return render(
    <FeatureFlagsContext.Provider value={flags}>
      <BetaBanner />
    </FeatureFlagsContext.Provider>
  )
}

describe('BetaBanner', () => {
  it('renders nothing when enable_beta_banner is false', () => {
    const { container } = renderWithFlags({ enable_beta_banner: false })

    expect(container).toBeEmptyDOMElement()
  })

  it('renders an info alert when enable_beta_banner is true', () => {
    renderWithFlags({ enable_beta_banner: true })

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveClass('usa-alert--info')
  })

  it('renders localized banner text', () => {
    renderWithFlags({ enable_beta_banner: true })

    // i18n key: betaBannerText (common namespace)
    // Falls back to the default English string until the key is in the spreadsheet
    expect(
      screen.getByText(
        'This site is currently in beta. Some features may be incomplete or missing.'
      )
    ).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/SEBT.Portal.Web && pnpm vitest run src/components/BetaBanner.test.tsx`

Expected: FAIL — `BetaBanner` module does not exist yet.

- [ ] **Step 3: Commit**

```
test: add failing tests for BetaBanner component
```

---

### Task 3: Implement BetaBanner component

**Files:**
- Create: `src/SEBT.Portal.Web/src/components/BetaBanner.tsx`

- [ ] **Step 1: Write the component**

```tsx
'use client'

import { useFeatureFlag } from '@/features/feature-flags'
import { Alert } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

export function BetaBanner() {
  const enabled = useFeatureFlag('enable_beta_banner')
  const { t } = useTranslation('common')

  if (!enabled) {
    return null
  }

  return (
    <Alert variant="info">
      {t(
        'betaBannerText',
        'This site is currently in beta. Some features may be incomplete or missing.'
      )}
    </Alert>
  )
}
```

- [ ] **Step 2: Run the tests to verify they pass**

Run: `cd src/SEBT.Portal.Web && pnpm vitest run src/components/BetaBanner.test.tsx`

Expected: PASS — all 3 tests green.

- [ ] **Step 3: Commit**

```
feat: implement BetaBanner component
```

---

### Task 4: Add BetaBanner to root layout

**Files:**
- Modify: `src/SEBT.Portal.Web/src/app/layout.tsx:1-2` (imports)
- Modify: `src/SEBT.Portal.Web/src/app/layout.tsx:119-120` (render)

- [ ] **Step 1: Add import to layout.tsx**

Add to the imports at the top of `src/SEBT.Portal.Web/src/app/layout.tsx`:

```typescript
import { BetaBanner } from '@/components/BetaBanner'
```

- [ ] **Step 2: Insert BetaBanner between site-alerts and Header**

In the JSX, between `<div id="site-alerts" />` and `<Header state={state} />`:

```tsx
                    <div id="site-alerts" />
                    <BetaBanner />
                    <Header state={state} />
```

- [ ] **Step 3: Run frontend tests to check for regressions**

Run: `cd src/SEBT.Portal.Web && pnpm vitest run`

Expected: All tests pass (including BetaBanner tests).

- [ ] **Step 4: Run lint to check for issues**

Run: `cd src/SEBT.Portal.Web && pnpm lint`

Expected: No errors.

- [ ] **Step 5: Commit**

```
feat: render BetaBanner in root layout above Header
```

---

### Task 5: Manual verification

- [ ] **Step 1: Start the dev server with STATE=co**

Run: `STATE=co pnpm dev`

- [ ] **Step 2: Verify banner appears**

Open the portal in a browser. The info alert banner should appear above the state header on:
- The landing/login page (public route)
- The dashboard (authenticated route)

- [ ] **Step 3: Verify banner is hidden with STATE=dc**

Restart with: `STATE=dc pnpm dev`

The banner should NOT appear (DC's config doesn't include `enable_beta_banner: true`).

- [ ] **Step 4: Note content gap**

The i18n key `betaBannerText` in the `common` namespace needs to be added to the source Google Sheet and the CSV re-exported. Until then the `t()` fallback string provides the English text. File a follow-up or add to the content backlog.
