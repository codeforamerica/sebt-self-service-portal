import { expect, test } from '@playwright/test'

/**
 * Example E2E Test - SEBT Portal Homepage
 *
 * Tests basic functionality and accessibility of the homepage
 */
test.describe('Homepage', () => {
  test('should load and display the homepage', async ({ page }) => {
    await page.goto('/')

    // The homepage currently redirects to /login
    await expect(page).toHaveURL(/\/login\/?$/)

    // Check that metadata loaded (state-based title)
    await expect(page).toHaveTitle(/SUN Bucks/i)

    const continueButton = page.getByRole('button', { name: /continue/i })
    await expect(continueButton).toBeVisible()

    // Check for USWDS JavaScript initialization
    const html = page.locator('html')
    await expect(html).not.toHaveClass(/usa-js-loading/, { timeout: 10000 })
  })

  test('should have proper USWDS state attribute', async ({ page }) => {
    await page.goto('/')

    // Check that state attribute is set (DC or CO)
    const html = page.locator('html')
    const stateAttr = await html.getAttribute('data-state')
    expect(['dc', 'co']).toContain(stateAttr)
  })

  test('should be accessible', async ({ page }) => {
    await page.goto('/')

    // Basic accessibility check - ensure main landmark exists
    const main = page.locator('main, [role="main"]')
    await expect(main).toBeVisible()
  })

  test('switches language to Spanish and shows translated UI', async ({ page }) => {
    await page.goto('/login')

    // Initially loads in English
    await expect(page.getByRole('button', { name: 'Continue' })).toBeVisible()

    // Switch to Spanish (handles both desktop + mobile language selector variants)
    const desktopSelector = page.locator('.usa-language__desktop')
    if (await desktopSelector.isVisible()) {
      await page.locator('.usa-language__desktop button[lang="es"]').click()
    } else {
      await page.locator('button[aria-controls="language-options"]').click()
      await page.locator('button[role="menuitem"][lang="es"]').click()
    }

    // Assert at least one string is translated
    await expect(page.getByRole('button', { name: 'Continuar' })).toBeVisible()
  })
})
