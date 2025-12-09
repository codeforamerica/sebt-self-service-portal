import { expect, test } from '@playwright/test'

/**
 * Example E2E Test - SEBT Portal Homepage
 *
 * Tests basic functionality and accessibility of the homepage
 */
test.describe('Homepage', () => {
  test('should load and display the homepage', async ({ page }) => {
    await page.goto('/')

    // Check that the page loaded
    await expect(page).toHaveTitle(/SEBT Portal/i)

    // Check for USWDS JavaScript initialization
    const html = page.locator('html')
    await expect(html).not.toHaveClass(/usa-js-loading/)
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
})
