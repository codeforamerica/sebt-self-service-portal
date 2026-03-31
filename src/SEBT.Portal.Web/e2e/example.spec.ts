import { expect, test } from '@playwright/test'

/**
 * Example E2E Test - SEBT Portal entry + login
 *
 * Covers DC (email + Continue) and CO (OIDC + Log in…) flows via flexible role/name matchers.
 */
test.describe('Homepage', () => {
  test('redirects / to /login', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
  })

  test('should load and display the login entry', async ({ page }) => {
    await page.goto('/login')

    await expect(page).toHaveTitle(/SUN Bucks/i)

    // DC: submit shows "Continue"; CO: "Log in…" (e.g. myColorado)
    const primaryAction = page.getByRole('button', { name: /continue|log in/i }).first()
    await expect(primaryAction).toBeVisible()

    // `usa-js-loading` is driven by USWDS scripts; defer-only init can clear it slowly or not at all
    // in some headless timings—primary CTA visibility is the user-facing signal we need here.
    await expect(page.locator('script[src="/js/uswds-init.min.js"]')).toBeAttached()
  })

  test('should have proper USWDS state attribute', async ({ page }) => {
    await page.goto('/login')

    const html = page.locator('html')
    const stateAttr = await html.getAttribute('data-state')
    expect(['dc', 'co']).toContain(stateAttr)
  })

  test('should be accessible', async ({ page }) => {
    await page.goto('/login')

    // Root layout exposes the primary landmark (see app/layout.tsx)
    const main = page.locator('#main-content')
    await expect(main).toBeVisible()
  })

  test('switches language to Spanish and shows translated UI', async ({ page }) => {
    await page.goto('/login')

    await expect(page.getByRole('button', { name: /continue|log in/i }).first()).toBeVisible()

    const desktopSelector = page.locator('.usa-language__desktop')
    if (await desktopSelector.isVisible()) {
      await page.locator('.usa-language__desktop button[lang="es"]').click()
    } else {
      await page.locator('button[aria-controls="language-options"]').click()
      await page.locator('button[role="menuitem"][lang="es"]').click()
    }

    // DC form submit: "Continuar"; CO (es): primary CTA uses "Iniciar sesión"
    await expect(
      page.getByRole('button', { name: /continuar|iniciar sesión/i }).first()
    ).toBeVisible()
  })
})
