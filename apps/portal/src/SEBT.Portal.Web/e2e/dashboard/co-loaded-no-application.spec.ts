import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'

test.describe('Dashboard action buttons', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({ applications: [] })
    })
  })

  test('hides Check existing applications when the household has no applications (DC-402)', async ({
    page
  }) => {
    await page.goto('/dashboard')

    await expect(page.getByRole('link', { name: /check existing cards/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /check existing applications/i })).toHaveCount(0)
  })
})
