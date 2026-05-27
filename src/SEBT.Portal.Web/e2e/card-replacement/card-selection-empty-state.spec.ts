import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData, makeSummerEbtCase, recentCardDate } from '../fixtures/household-data'

// DC-357: when client-side filters eliminate every case on /cards/request,
// the page used to render only an info alert. Users had no way back.
// These tests guard against the regression by asserting the Back button is
// rendered and returns the user to the dashboard for both empty-state branches.

test.describe('CardSelection empty state has a back button', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
  })

  test('cooldown branch: shows alert + back button, back returns to dashboard', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({
        summerEbtCases: [makeSummerEbtCase({ issuanceType: 1, cardRequestedAt: recentCardDate() })]
      })
    })

    await page.goto('/dashboard')
    await page.goto('/cards/request')

    await expect(page.getByText(/recently replaced/i)).toBeVisible()

    const backButton = page.getByRole('button', { name: /back/i })
    await expect(backButton).toBeVisible()
    await backButton.click()

    await expect(page).toHaveURL('/dashboard')
  })

  test('no-children branch: shows alert + back button, back returns to dashboard', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({ summerEbtCases: [] })
    })

    await page.goto('/dashboard')
    await page.goto('/cards/request')

    await expect(page.getByText(/no children found/i)).toBeVisible()

    const backButton = page.getByRole('button', { name: /back/i })
    await expect(backButton).toBeVisible()
    await backButton.click()

    await expect(page).toHaveURL('/dashboard')
  })
})
