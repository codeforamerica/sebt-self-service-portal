import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import {
  makeApplication,
  makeHouseholdData,
  makeSummerEbtCase,
  OLD_CARD_DATE
} from '../fixtures/household-data'
import { currentState } from '../fixtures/state'

const ADDRESS_FORM_DATA =
  currentState === 'co'
    ? { street: '200 E Colfax Ave', city: 'Denver', state: 'CO', zip: '80203' }
    : {
        street: '456 Oak Avenue NW',
        city: 'Washington',
        state: 'DC',
        zip: '20002'
      }

const EXPECTED_PREFILL =
  currentState === 'co'
    ? { street: '200 E Colfax Ave', city: 'Denver', zip: '80203' }
    : { street: '1350 Pennsylvania Ave NW', city: 'Washington', zip: '20004' }

/**
 * Fills and submits the address form with valid data.
 * The form submits via PUT /api/household/address, then stores the address
 * in AddressFlowContext (React state only) and navigates to the next step.
 */
async function fillAndSubmitAddressForm(page: import('@playwright/test').Page) {
  await page.fill('[name="streetAddress1"]', ADDRESS_FORM_DATA.street)
  await page.fill('[name="city"]', ADDRESS_FORM_DATA.city)
  await page.selectOption('[name="state"]', ADDRESS_FORM_DATA.state)
  await page.fill('[name="postalCode"]', ADDRESS_FORM_DATA.zip)
  await page.getByRole('button', { name: 'Continue' }).click()
}

test.describe('Address update flow', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({
        summerEbtCases: [makeSummerEbtCase({ issuanceType: 1 })],
        applications: [makeApplication({ cardRequestedAt: OLD_CARD_DATE, issuanceType: 1 })]
      })
    })
  })

  test('address form renders with pre-filled data from household API', async ({ page }) => {
    await page.goto('/profile/address')
    await expect(page.locator('[name="streetAddress1"]')).toHaveValue(EXPECTED_PREFILL.street)
    await expect(page.locator('[name="city"]')).toHaveValue(EXPECTED_PREFILL.city)
    await expect(page.locator('[name="postalCode"]')).toHaveValue(EXPECTED_PREFILL.zip)
  })

  test('address form submission navigates to replacement card prompt', async ({ page }) => {
    await page.goto('/profile/address')
    await fillAndSubmitAddressForm(page)
    await expect(page).toHaveURL('/profile/address/replacement-cards')
  })

  test.describe('ReplacementCardPrompt', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/profile/address')
      await fillAndSubmitAddressForm(page)
      await expect(page).toHaveURL('/profile/address/replacement-cards')
    })

    test('shows validation error when submitted without a selection', async ({ page }) => {
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page.locator('.usa-error-message')).toBeVisible()
    })

    test('"No" selection navigates to dashboard and shows address updated alert', async ({
      page
    }) => {
      // USWDS radio tiles use visually-hidden inputs. Click the label —
      // it's the visible, clickable target that scrolls into view reliably.
      await page.locator('label[for="replacement-no"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      // DashboardAlerts captures the addressUpdated param in state then cleans the URL.
      await expect(page).toHaveURL('/dashboard')
      await expect(
        page.locator('.usa-alert--success', { hasText: 'Address update recorded' })
      ).toBeVisible()
    })

    test('"Yes" selection navigates to card selection', async ({ page }) => {
      await page.locator('label[for="replacement-yes"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL('/profile/address/replacement-cards/select')
    })
  })

  test.describe('CardSelection', () => {
    test.beforeEach(async ({ page }) => {
      // Navigate through the flow to reach card selection
      await page.goto('/profile/address')
      await fillAndSubmitAddressForm(page)
      await expect(page).toHaveURL('/profile/address/replacement-cards')
      await page.locator('label[for="replacement-yes"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL('/profile/address/replacement-cards/select')
    })

    test('shows validation error when submitted without selecting a card', async ({ page }) => {
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page.locator('.usa-error-message')).toBeVisible()
    })

    test('selecting a card and continuing navigates to confirm page', async ({ page }) => {
      // Click the label — USWDS checkbox tiles use visually-hidden inputs.
      await page.locator('.usa-checkbox__label').first().click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL(/\/profile\/address\/replacement-cards\/select\/confirm\?cases=/)
    })
  })

  test.describe('ConfirmRequest (address flow)', () => {
    test.beforeEach(async ({ page }) => {
      // Navigate through the full address + card selection flow
      await page.goto('/profile/address')
      await fillAndSubmitAddressForm(page)
      await page.locator('label[for="replacement-yes"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await page.locator('.usa-checkbox__label').first().click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL(/\/profile\/address\/replacement-cards\/select\/confirm/)
    })

    test('shows card order summary with child name', async ({ page }) => {
      await expect(page.getByText("John Doe's card")).toBeVisible()
    })

    test('shows mailing address', async ({ page }) => {
      // The submitted address (from the form, not the household data address on file)
      await expect(page.locator('address')).toContainText(ADDRESS_FORM_DATA.street)
    })

    test('"Order card" button posts to replace endpoint and shows success alert', async ({
      page
    }) => {
      await page.getByRole('button', { name: 'Order card' }).click()
      // DashboardAlerts captures flash param in state then cleans the URL.
      await expect(page).toHaveURL('/dashboard')
      await expect(
        page.locator('.usa-alert--success', {
          hasText: 'Your replacement card request has been recorded'
        })
      ).toBeVisible()
    })
  })
})

test.describe('Address flow: direct navigation guards', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page)
  })

  test('accessing /profile/address/replacement-cards directly redirects to address form', async ({
    page
  }) => {
    // AddressFlowContext is empty on direct load — FlowGuard redirects to the form
    await page.goto('/profile/address/replacement-cards')
    await expect(page).toHaveURL('/profile/address')
  })

  test('accessing /profile/address/replacement-cards/select directly redirects to address form', async ({
    page
  }) => {
    await page.goto('/profile/address/replacement-cards/select')
    await expect(page).toHaveURL('/profile/address')
  })
})
