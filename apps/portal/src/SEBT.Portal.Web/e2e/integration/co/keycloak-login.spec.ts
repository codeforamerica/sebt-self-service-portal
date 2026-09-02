import { expect, test, type Page } from '@playwright/test'

// Full-stack Colorado sign-in through the local Keycloak stand-in for
// myColorado — a real OIDC redirect dance with credentials, not cookie
// injection. Requires: API in CO config (the appsettings.co overlay plus the
// Keycloak OIDC settings), web app in CO config, and the compose `keycloak`
// profile running (see docs/development/keycloak-oidc.md).
//
// Colorado admits a base myColorado login straight to the dashboard
// (household+view is IAL1); the step-up client only gates address and card
// actions, which the mocked IalGuard specs cover.
const CO_LOADED_EMAIL = 'sebt.co+co-loaded@codeforamerica.org'
const KEYCLOAK_PASSWORD = 'password'

async function signInAtKeycloak(page: Page): Promise<void> {
  await page.waitForURL('**/realms/sebt/**', { timeout: 30_000 })
  await page.locator('#username').fill(CO_LOADED_EMAIL)
  await page.locator('#password').fill(KEYCLOAK_PASSWORD)
  await page.locator('#kc-login').click()
}

test.describe('Colorado myColorado (Keycloak) sign-in', () => {
  test('guardian signs in, reaches the dashboard, and signs out', async ({ page }) => {
    // ── Sign in: portal → IdP credentials → OIDC callback → session ──
    await page.goto('/login')
    await page.getByRole('button', { name: /sign in with mycolorado/i }).click()
    await signInAtKeycloak(page)

    // ── Dashboard: the co-loaded household resolves by the phone claim ──
    await page.waitForURL('**/dashboard**', { timeout: 30_000 })
    await expect(page.getByRole('heading', { level: 1, name: /dashboard/i })).toBeAttached()
    // The mock co-loaded household's children render — not the empty state.
    await expect(page.locator('#enrolled-children-heading')).toBeVisible({ timeout: 15_000 })
    await expect(page.getByText(/no children enrolled/i)).toHaveCount(0)
    const logoutLink = page.getByRole('link', { name: /sign out|log out|logout/i })
    await expect(logoutLink).toBeVisible()

    // ── Sign out: revokes the portal session and rides the IdP's
    // end-session redirect back to the login page. The logout request carries
    // no id_token_hint, so Keycloak interposes a confirmation screen; a real
    // guardian clicks through it the same way. ──
    await logoutLink.click()
    await page.waitForURL('**/protocol/openid-connect/logout**', { timeout: 30_000 })
    await page.getByRole('button', { name: /^logout$/i }).click()
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 30_000 })
    await expect(page.getByRole('button', { name: /sign in with mycolorado/i })).toBeVisible()

    // The revoked session cannot re-enter the dashboard.
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
  })
})
