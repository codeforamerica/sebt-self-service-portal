import type { Page } from '@playwright/test'

import { MOCK_JWT } from './household-data'

/**
 * Name of the HttpOnly session cookie set by the backend (see AuthCookies.AuthCookieName).
 * The browser sends it automatically; client-side JS cannot read it.
 */
const SESSION_COOKIE_NAME = 'sebt_portal_session'

/**
 * Seeds an authenticated session for E2E tests by writing the HttpOnly session cookie
 * directly into the browser context. The backend is intercepted by setupApiRoutes (which
 * also mocks /auth/status), so the JWT value itself is never validated — only its
 * presence on the request matters for the cookie path on the JwtBearer middleware.
 *
 * Must be called before page.goto() so the cookie exists for the first navigation.
 */
export async function injectAuth(page: Page, token = MOCK_JWT): Promise<void> {
  // Mirrors playwright.config.ts so the cookie matches the page origin.
  const baseURL = process.env.BASE_URL || 'http://localhost:3000'
  await page.context().addCookies([
    {
      name: SESSION_COOKIE_NAME,
      value: token,
      url: baseURL,
      httpOnly: true,
      // E2E runs against http://localhost — Secure must be false or the browser drops the cookie.
      secure: false,
      sameSite: 'Lax'
    }
  ])
}
