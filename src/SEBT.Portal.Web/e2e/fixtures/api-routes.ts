import type { Page } from '@playwright/test'

import { DEFAULT_FEATURE_FLAGS, makeHouseholdData, type MockHouseholdData } from './household-data'

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
  const householdData = overrides.householdData ?? makeHouseholdData()
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

  // Provide an authenticated session for AuthContext — IAL/id-proofing claims
  // satisfy the CO step-up gate; DC ignores them.
  await page.route('**/api/auth/status', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        isAuthorized: true,
        email: 'e2e@example.com',
        ial: '1plus',
        idProofingStatus: 2,
        // ~Apr 2026; stays fresh inside the 5-year window
        idProofingCompletedAt: 1775000000,
        idProofingExpiresAt: null
      })
    })
  })

  // Keep the mock session alive — a 401 here would clear local state and redirect to /login.
  // New contract: 204 No Content + Set-Cookie (cookie value is opaque to the SPA).
  await page.route('**/api/auth/refresh', (route) => {
    void route.fulfill({ status: 204 })
  })

  await page.route('**/api/household/data', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(householdData)
    })
  })

  await page.route('**/api/features', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(featureFlags)
    })
  })

  await page.route('**/api/household/address', (route) => {
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
