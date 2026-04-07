import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeApplication, makeHouseholdData, OLD_CARD_DATE } from '../fixtures/household-data'
import { currentState } from '../fixtures/state'

const APP_NUMBER = 'APP-2026-001'
const EXPECTED_ADDRESS = currentState === 'co' ? '200 E Colfax Ave' : '1350 Pennsylvania Ave NW'
const ENCODED_APP = encodeURIComponent(APP_NUMBER)

test.describe('Standalone replacement flow', () => {
  test.describe('ConfirmAddress (/cards/replace)', () => {
    test.beforeEach(async ({ page }) => {
      await injectAuth(page)
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({ applicationNumber: APP_NUMBER, cardRequestedAt: OLD_CARD_DATE })
          ]
        })
      })
    })

    test('renders confirm address page with address on file', async ({ page }) => {
      await page.goto(`/cards/replace?app=${ENCODED_APP}`)
      await expect(page.locator('h1')).toContainText(
        'Do you want the new card mailed to this address?'
      )
      await expect(page.getByText(EXPECTED_ADDRESS)).toBeVisible()
    })

    test('shows validation error when submitted without a selection', async ({ page }) => {
      await page.goto(`/cards/replace?app=${ENCODED_APP}`)
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page.locator('.usa-error-message')).toBeVisible()
    })

    test('"Yes" navigates to confirm request page', async ({ page }) => {
      await page.goto(`/cards/replace?app=${ENCODED_APP}`)
      // USWDS radio tiles use visually-hidden inputs. Click the label instead —
      // it's the visible, clickable target and scrolls into view reliably.
      await page.locator('label[for="confirm-address-yes"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL(`/cards/replace/confirm?app=${ENCODED_APP}`)
    })

    test('"No" navigates to address change page', async ({ page }) => {
      await page.goto(`/cards/replace?app=${ENCODED_APP}`)
      await page.locator('label[for="confirm-address-no"]').click()
      await page.getByRole('button', { name: 'Continue' }).click()
      await expect(page).toHaveURL(`/cards/replace/address?app=${ENCODED_APP}`)
    })

    test('missing app param redirects to dashboard', async ({ page }) => {
      // CardReplaceLayout redirects to /dashboard when ?app is missing.
      await page.goto('/cards/replace')
      await expect(page).toHaveURL('/dashboard')
    })

    test('shows error alert when app param does not match any application', async ({ page }) => {
      await page.goto('/cards/replace?app=NONEXISTENT')
      await expect(page.locator('.usa-alert--error')).toBeVisible()
    })
  })

  test.describe('ConfirmRequest (/cards/replace/confirm)', () => {
    test.beforeEach(async ({ page }) => {
      await injectAuth(page)
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({ applicationNumber: APP_NUMBER, cardRequestedAt: OLD_CARD_DATE })
          ]
        })
      })
    })

    test('renders confirm request page with child name and address', async ({ page }) => {
      await page.goto(`/cards/replace/confirm?app=${ENCODED_APP}`)
      await expect(page.getByText("John Doe's card")).toBeVisible()
      await expect(page.locator('address')).toContainText(EXPECTED_ADDRESS)
    })

    test('"Order card" posts to replace endpoint and redirects to dashboard with success alert', async ({
      page
    }) => {
      await page.goto(`/cards/replace/confirm?app=${ENCODED_APP}`)
      await page.getByRole('button', { name: 'Order card' }).click()
      // DashboardAlerts captures the flash param in state then calls router.replace() to clean the URL.
      // Assert on the final URL (param removed) and that the alert is visible.
      await expect(page).toHaveURL('/dashboard')
      await expect(
        page.locator('.usa-alert--success', {
          hasText: 'Your replacement card request has been recorded'
        })
      ).toBeVisible()
    })

    test('shows error alert on replace API failure', async ({ page }) => {
      // Re-setup routes with cardReplaceStatus: 500 for this test.
      // injectAuth/setupApiRoutes from beforeEach are already registered —
      // Playwright matches routes in registration order, most recent first,
      // so this override takes precedence for the replace endpoint.
      await page.route('**/api/household/cards/replace', (route) => {
        void route.fulfill({ status: 500 })
      })
      await page.goto(`/cards/replace/confirm?app=${ENCODED_APP}`)
      await page.getByRole('button', { name: 'Order card' }).click()
      await expect(page.locator('.usa-alert--error')).toBeVisible()
    })

    test('missing app param redirects to dashboard', async ({ page }) => {
      // CardReplaceLayout redirects to /dashboard when ?app is missing.
      await page.goto('/cards/replace/confirm')
      await expect(page).toHaveURL('/dashboard')
    })
  })
})
