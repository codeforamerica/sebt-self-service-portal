/**
 * Regression: after a successful address update, Back shows the new address on the dashboard.
 */
import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import {
  makeApplication,
  makeHouseholdData,
  makeSummerEbtCase,
  OLD_CARD_DATE
} from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

const OLD_STREET = '1350 Pennsylvania Ave NW'
const NEW_STREET = '456 Oak Avenue NW'

test.describe('Address update back navigation cache (DC)', () => {
  test.beforeEach(() => {
    skipUnlessState('dc')
  })

  test('dashboard shows updated addressOnFile after update then Back', async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({
        summerEbtCases: [makeSummerEbtCase({ issuanceType: 1, cardRequestedAt: OLD_CARD_DATE })],
        applications: [makeApplication({ issuanceType: 1 })]
      })
    })

    await page.goto('/dashboard')
    await expect(page.getByText(OLD_STREET)).toBeVisible()

    await page.goto('/profile/address')
    await page.fill('[name="streetAddress1"]', NEW_STREET)
    await page.fill('[name="city"]', 'Washington')
    await page.selectOption('[name="state"]', 'DC')
    await page.fill('[name="postalCode"]', '20002')
    await page.getByRole('button', { name: 'Continue' }).click()
    await expect(page).toHaveURL('/profile/address/replacement-cards')
    await expect(page.getByText(NEW_STREET)).toBeVisible()

    await page.getByRole('button', { name: 'Back' }).click()
    await expect(page).toHaveURL('/dashboard')

    await expect(page.getByText(NEW_STREET)).toBeVisible()
    await expect(page.getByText(OLD_STREET)).not.toBeVisible()
  })
})
