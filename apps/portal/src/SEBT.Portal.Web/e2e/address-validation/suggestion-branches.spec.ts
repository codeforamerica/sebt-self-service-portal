import { expect, test, type Page, type Route } from '@playwright/test'

import { fillAndSubmitAddressForm } from '../fixtures/address-form'
import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'
import { currentState } from '../fixtures/state'

const ENTERED =
  currentState === 'co'
    ? { street: '200 E Colfax Ave', city: 'Denver', state: 'CO', zip: '80203' }
    : { street: '456 Oak Avenue NW', city: 'Washington', state: 'DC', zip: '20002' }

const SUGGESTED =
  currentState === 'co'
    ? { streetAddress1: '200 East Colfax Avenue', streetAddress2: null, city: 'Denver', state: 'CO', postalCode: '80203-1716' }
    : { streetAddress1: '456 Oak Ave NW', streetAddress2: null, city: 'Washington', state: 'DC', postalCode: '20002-1122' }

/**
 * Registers a PUT /household/address handler where the first submit returns a
 * Smarty-style suggestion and any follow-up submit (from the suggestion screen)
 * succeeds. Registered after setupApiRoutes so it wins over the fixture's
 * static address handler. Returns the recorded follow-up request bodies.
 */
async function mockSuggestionThenValid(page: Page): Promise<Array<Record<string, unknown>>> {
  const followUpBodies: Array<Record<string, unknown>> = []
  let puts = 0
  await page.route('**/api/household/address*', (route: Route) => {
    if (route.request().method() !== 'PUT') return route.fallback()
    puts += 1
    if (puts === 1) {
      // 422, matching HouseholdController: any address that isn't valid — suggestion
      // included — comes back as UnprocessableEntity carrying the structured body.
      // The client only unwraps a suggestion from the 422 branch, so mocking 200 here
      // would skip that path entirely.
      return route.fulfill({
        status: 422,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'suggestion', reason: 'suggested', suggestedAddress: SUGGESTED })
      })
    }
    followUpBodies.push(route.request().postDataJSON() as Record<string, unknown>)
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'valid' })
    })
  })
  return followUpBodies
}

test.describe('Address suggestion branches', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, { householdData: makeHouseholdData() })
  })

  test('accepting the suggestion submits the suggested address and continues', async ({ page }) => {
    const followUps = await mockSuggestionThenValid(page)

    await page.goto('/profile/address')
    await fillAndSubmitAddressForm(page, ENTERED)

    await page.waitForURL('**/profile/address/suggested-address')
    // Both candidates are on screen; the suggested one is preselected.
    await expect(page.getByText(SUGGESTED.streetAddress1)).toBeVisible()
    await expect(page.getByText(ENTERED.street)).toBeVisible()
    await expect(page.locator('#address-suggested')).toBeChecked()

    await page.getByRole('button', { name: 'Continue' }).click()

    // Continues into the replacement-card prompt with the suggested values persisted.
    await page.waitForURL('**/profile/address/replacement-cards')
    expect(followUps).toHaveLength(1)
    expect(followUps[0]).toMatchObject({ streetAddress1: SUGGESTED.streetAddress1 })
    expect(followUps[0]).not.toMatchObject({ acceptEnteredAddress: true })
  })

  test('keeping the entered address resubmits it with acceptEnteredAddress', async ({ page }) => {
    const followUps = await mockSuggestionThenValid(page)

    await page.goto('/profile/address')
    await fillAndSubmitAddressForm(page, ENTERED)

    await page.waitForURL('**/profile/address/suggested-address')
    // USWDS radios visually hide the input; click its label.
    await page.locator('label[for="address-entered"]').click()
    await expect(page.locator('#address-entered')).toBeChecked()
    await page.getByRole('button', { name: 'Continue' }).click()

    await page.waitForURL('**/profile/address/replacement-cards')
    expect(followUps).toHaveLength(1)
    expect(followUps[0]).toMatchObject({
      streetAddress1: ENTERED.street,
      acceptEnteredAddress: true
    })
  })
})
