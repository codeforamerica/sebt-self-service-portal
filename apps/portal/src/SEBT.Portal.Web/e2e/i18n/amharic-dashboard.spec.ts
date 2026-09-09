import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

// Amharic (DC-only) is verified with a screenshot diff rather than string
// asserts — the team has no Amharic speakers, so the pixels are the contract:
// any rendering or copy regression shows up against the committed baseline.
// Baselines are per-platform. Regenerate with `--update-snapshots=all`: the
// default mode only rewrites a baseline whose diff already exceeds
// maxDiffPixelRatio, so a small contaminant (a dev-tools overlay, say) survives
// a plain `--update-snapshots`.
// For the Linux baseline CI compares against, run the server *inside* the
// Playwright container rather than pointing it at a host server — a split
// host/container origin never reaches the authenticated dashboard and silently
// captures the sign-in page instead.
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
    // Prove we're on the authenticated dashboard before capturing. Without this the
    // screenshot silently accepts whatever rendered — an unauthenticated run captures
    // the sign-in page, and because the baseline is generated the same way, the diff
    // still passes while asserting nothing about the dashboard.
    await expect(page.locator('#enrolled-children-heading')).toBeVisible({ timeout: 20_000 })
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
