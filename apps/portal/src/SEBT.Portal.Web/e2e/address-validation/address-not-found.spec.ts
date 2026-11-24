import { expect, test } from '@playwright/test'

import { fillAndSubmitAddressForm } from '../fixtures/address-form'
import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'
import { forCurrentState, type StateCode } from '../fixtures/state'

const ENTERED_BY_STATE: Record<
  StateCode,
  { street: string; city: string; state: string; zip: string }
> = {
  dc: { street: '1 Nowhere Rd NW', city: 'Washington', state: 'DC', zip: '20002' },
  co: { street: '1 Nowhere Rd', city: 'Denver', state: 'CO', zip: '80203' }
}
const ENTERED = forCurrentState(ENTERED_BY_STATE)

test.describe('Address not found branch', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    // The fixture's 422 default body is the not-found validation response.
    await setupApiRoutes(page, {
      householdData: makeHouseholdData(),
      addressUpdateStatus: 422
    })
  })

  test('unverifiable address lands on the not-found screen', async ({ page }) => {
    await page.goto('/profile/address')
    await fillAndSubmitAddressForm(page, ENTERED)

    await page.waitForURL('**/profile/address/address-not-found')
    await expect(
      page.getByRole('heading', { name: /are you sure this address is correct/i })
    ).toBeVisible()
  })

  test('editing from the not-found screen returns to the form with the entered values', async ({
    page
  }) => {
    await page.goto('/profile/address')
    await fillAndSubmitAddressForm(page, ENTERED)
    await page.waitForURL('**/profile/address/address-not-found')

    await page.getByRole('button', { name: /edit the address/i }).click()

    await page.waitForURL('**/profile/address')
    await expect(page.locator('[name="streetAddress1"]')).toHaveValue(ENTERED.street)
  })
})
