import type { Page } from '@playwright/test'

import {
  DEFAULT_FEATURE_FLAGS,
  MOCK_JWT,
  makeHouseholdData,
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
 * auth/refresh must be intercepted here: if it returns 401, apiFetch clears
 * the token and redirects to /login, which would break all authenticated tests.
 * We return a success response with the same mock token to keep the session alive.
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

  // Keep the mock session alive — a 401 here would clear the token and redirect to /login.
  await page.route('**/api/auth/refresh', (route) => {
    void route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: MOCK_JWT, requiresIdProofing: false })
    })
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
