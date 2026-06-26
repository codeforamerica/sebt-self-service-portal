/**
 * Regression: Back from suggested-address keeps the address the user entered.
 */
import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

const OLD_STREET = '1350 Pennsylvania Ave NW'
const TYPED_STREET = '456 Oak Avenue NW'

test.describe('Address form back from suggestion (DC)', () => {
  test.beforeEach(() => {
    skipUnlessState('dc')
  })

  test('form keeps entered address after suggestion screen Back', async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdData: makeHouseholdData(),
      addressUpdateStatus: 422,
      addressUpdateBody: {
        status: 'suggestion',
        reason: 'suggested',
        suggestedAddress: {
          streetAddress1: '456 OAK AVE NW',
          streetAddress2: null,
          city: 'Washington',
          state: 'DC',
          postalCode: '20002'
        }
      }
    })

    await page.goto('/profile/address')
    await page.fill('[name="streetAddress1"]', TYPED_STREET)
    await page.getByRole('button', { name: 'Continue' }).click()
    await expect(page).toHaveURL('/profile/address/suggested-address')

    await page.getByRole('button', { name: 'Back' }).click()
    await expect(page).toHaveURL('/profile/address')

    await expect(page.locator('[name="streetAddress1"]')).toHaveValue(TYPED_STREET)
    await expect(page.locator('[name="streetAddress1"]')).not.toHaveValue(OLD_STREET)
  })
})
