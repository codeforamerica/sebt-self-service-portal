import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'

test.describe('Dashboard insufficient IAL redirect', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdDataStatus: 403,
      householdDataProblem: {
        type: 'about:blank',
        title: 'Insufficient IAL',
        status: 403,
        requiredIal: 'IAL1plus'
      }
    })
  })

  test('redirects to id-proofing when household data returns 403 with requiredIal', async ({
    page
  }) => {
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login\/id-proofing\/?$/, { timeout: 15_000 })
    await expect(page.locator('#id-proofing-title')).toBeVisible()
  })
})
