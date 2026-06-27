import type { Page } from '@playwright/test'

import {
  DEFAULT_FEATURE_FLAGS,
  makeHouseholdData,
  MOCK_USER_ID,
  type MockHouseholdData
} from './household-data'

interface ApiRouteOverrides {
  /** Override the household data response. Defaults to makeHouseholdData(). */
  householdData?: MockHouseholdData
  /** Override specific feature flags. Merged with DEFAULT_FEATURE_FLAGS. */
  featureFlags?: Partial<typeof DEFAULT_FEATURE_FLAGS>
  /**
   * Override the PUT /api/household/address response.
   * Defaults to 200 with { status: 'valid' }.
   * Use addressUpdateBody to customize the JSON payload.
   */
  addressUpdateStatus?: number
  /**
   * Override the PUT /api/household/address response body.
   * Defaults to { status: 'valid' } for 200, or
   * { status: 'invalid', reason: 'not-found', message: 'Address not found' } for 422.
   * Set to null for no body (e.g., 204).
   */
  addressUpdateBody?: Record<string, unknown> | null
  /**
   * Override the POST /api/household/cards/replace response status.
   * Defaults to 204 (success).
   */
  cardReplaceStatus?: number
  /** Override GET /api/household/data response status. Defaults to 200. */
  householdDataStatus?: number
  /**
   * JSON body when householdDataStatus is not 200.
   * For 403 IAL step-up, include `requiredIal` (see useHouseholdData).
   */
  householdDataProblem?: Record<string, unknown>
}

/**
 * Intercepts all backend API calls and returns controlled mock responses.
 * Call before page.goto() — route handlers are registered at call time and
 * apply to all subsequent navigations on this page object.
 *
 * The Next.js proxy forwards /api/* to the backend. Playwright's page.route()
 * intercepts at the browser level, so it catches these proxied requests before
 * they leave the browser.
 *
 * auth/status drives the SPA's session — AuthContext queries it on mount and
 * after login/refresh. AuthGuard redirects to /login if it returns 401, so it
 * must be mocked as authenticated for all flows that depend on a logged-in user.
 *
 * auth/refresh must also be intercepted: a 401 here would log the user out.
 * The new contract is 204 No Content with a Set-Cookie header (the JWT lives
 * in the HttpOnly session cookie, not the response body).
 */
export async function setupApiRoutes(page: Page, overrides: ApiRouteOverrides = {}): Promise<void> {
  const householdSnapshot = structuredClone(overrides.householdData ?? makeHouseholdData())
  const featureFlags = { ...DEFAULT_FEATURE_FLAGS, ...(overrides.featureFlags ?? {}) }
  const addressUpdateStatus = overrides.addressUpdateStatus ?? 200
  const addressUpdateBody =
    overrides.addressUpdateBody !== undefined
      ? overrides.addressUpdateBody
      : addressUpdateStatus === 422
        ? { status: 'invalid', reason: 'not-found', message: 'Address not found' }
        : addressUpdateStatus === 200
          ? { status: 'valid' }
          : null
  const cardReplaceStatus = overrides.cardReplaceStatus ?? 204
  const householdDataStatus = overrides.householdDataStatus ?? 200
  const householdDataProblem = overrides.householdDataProblem ?? {
    type: 'about:blank',
    title: 'Forbidden',
    status: householdDataStatus
  }

  // The trailing `*` on GET routes tolerates the per-request `?_=<uuid>`
  // cache-bust query string that apiFetch appends to defeat edge-cache leaks
  // (see ADR 0016). Playwright glob `*` matches any characters except `/`,
  // so it equally matches the bare path and the path-plus-query form.

  // Provide an authenticated session for AuthContext — IAL/id-proofing claims
  // satisfy the CO step-up gate; DC ignores them.
  await page.route('**/api/auth/status*', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        isAuthorized: true,
        userId: MOCK_USER_ID,
        email: 'e2e@example.com',
        ial: '1plus',
        idProofingStatus: 2,
        // ~Apr 2026; stays fresh inside the 5-year window
        idProofingCompletedAt: 1775000000,
        // Server computes this from completedAt + ValidityDays; E2E must provide a future value
        idProofingExpiresAt: 1775000000 + 1826 * 86400
      })
    })
  })

  // Keep the mock session alive — a 401 here would clear local state and redirect to /login.
  // New contract: 204 No Content + Set-Cookie (cookie value is opaque to the SPA).
  await page.route('**/api/auth/refresh', (route) => {
    void route.fulfill({ status: 204 })
  })

  await page.route('**/api/household/data*', (route) => {
    if (householdDataStatus === 200) {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(householdSnapshot)
      })
      return
    }

    void route.fulfill({
      status: householdDataStatus,
      contentType: 'application/json',
      body: JSON.stringify(householdDataProblem)
    })
  })

  await page.route('**/api/features*', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(featureFlags)
    })
  })

  await page.route('**/api/household/address', async (route) => {
    if (route.request().method() === 'PUT' && addressUpdateStatus === 200) {
      try {
        const body = (await route.request().postDataJSON()) as {
          streetAddress1?: string
          streetAddress2?: string | null
          city?: string
          state?: string
          postalCode?: string
        }
        if (body.streetAddress1 && body.city && body.state && body.postalCode) {
          householdSnapshot.addressOnFile = {
            streetAddress1: body.streetAddress1,
            streetAddress2: body.streetAddress2 ?? null,
            city: body.city,
            state: body.state,
            postalCode: body.postalCode
          }
        }
      } catch {
        // Keep default snapshot when the request body is not JSON.
      }
    }

    void route.fulfill({
      status: addressUpdateStatus,
      ...(addressUpdateBody != null
        ? { contentType: 'application/json', body: JSON.stringify(addressUpdateBody) }
        : {})
    })
  })

  await page.route('**/api/household/cards/replace', (route) => {
    void route.fulfill({ status: cardReplaceStatus })
  })
}
