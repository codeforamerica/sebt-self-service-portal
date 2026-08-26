import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

// Amharic (DC-only) is verified with a screenshot diff rather than string
// asserts — the team has no Amharic speakers, so the pixels are the contract:
// any rendering or copy regression shows up against the committed baseline.
// Baselines are per-platform; regenerate with `--update-snapshots` (use the
// Playwright Docker image for the Linux baselines CI compares against).
test.describe('Amharic on the authenticated dashboard', () => {
  test.beforeEach(() => skipUnlessState('dc'))

  test('dashboard renders the Amharic layout', async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, { householdData: makeHouseholdData() })
    await page.addInitScript(() => localStorage.setItem('i18nextLng', 'am'))

    await page.goto('/dashboard')
    // Let network, fonts, and client-side layout settle before capturing — a
    // partially loaded font repaints every glyph and fails the whole diff.
    // Hydration can be slow on CI-class hardware, so give the language flip time.
    await expect(page.locator('html')).toHaveAttribute('lang', 'am', { timeout: 20_000 })
    await page.waitForLoadState('networkidle')
    await page.evaluate(() => document.fonts.ready)

    // Scope to the main content: the Amharic copy and layout live there, and
    // excluding header/footer imagery keeps the diff surface stable.
    await expect(page.getByRole('main')).toHaveScreenshot('dashboard-amharic.png', {
      // Tolerate minor anti-aliasing differences across environments while
      // still failing on real layout or copy changes.
      maxDiffPixelRatio: 0.02
    })
  })
})
