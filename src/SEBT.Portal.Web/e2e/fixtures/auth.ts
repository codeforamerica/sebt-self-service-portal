import type { Page } from '@playwright/test'

import { MOCK_JWT } from './household-data'

/**
 * The session storage key used by the auth context.
 * Must match AUTH_TOKEN_KEY in src/features/auth/context/AuthContext.tsx.
 */
const AUTH_TOKEN_KEY = 'auth_token'

/**
 * Injects a mock auth token into sessionStorage before the page loads.
 * Must be called before page.goto() because addInitScript runs at page creation.
 *
 * AuthGuard reads sessionStorage synchronously on hydration. Setting the token
 * via initScript ensures it's present before the React tree mounts, preventing
 * the redirect-to-login that would otherwise block navigation.
 */
export async function injectAuth(page: Page, token = MOCK_JWT): Promise<void> {
  await page.addInitScript(
    ({ key, value }) => {
      sessionStorage.setItem(key, value)
    },
    { key: AUTH_TOKEN_KEY, value: token }
  )
}
