import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import {
  makeCoLoadedOnlyHousehold,
  makeHouseholdData,
  makeSummerEbtCase
} from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

/**
 * Co-loaded (SNAP/TANF) UI behavior with mocked API routes.
 * Address/cards info pages are DC-only; skip on CO matrix runs.
 */
test.describe('Co-loaded dashboard and info pages', () => {
  test.beforeEach(() => {
    skipUnlessState('dc')
  })

  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
  })

  test('shows mailing-address info link when self-service address updates are denied', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      isCoLoaded: true,
      householdData: makeCoLoadedOnlyHousehold()
    })
    await page.goto('/dashboard')

    const addressInfoLink = page.getByRole('link', {
      name: /how we determine your mailing address/i
    })
    await expect(addressInfoLink).toBeVisible()
    await expect(addressInfoLink).toHaveAttribute('href', '/profile/address/info')
    await expect(page.getByRole('link', { name: /change my mailing address/i })).toHaveCount(0)
  })

  test('SNAP co-loaded case links replacement CTA to /cards/info', async ({ page }) => {
    await setupApiRoutes(page, {
      isCoLoaded: true,
      householdData: makeCoLoadedOnlyHousehold()
    })
    await page.goto('/dashboard')

    const link = page.locator('[data-testid="accordion-content"] a', {
      hasText: 'Request a replacement card'
    })
    await expect(link).toHaveAttribute('href', '/cards/info')
  })

  test('address info page explains SNAP/TANF mailing address and links to cards info', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      isCoLoaded: true,
      householdData: makeCoLoadedOnlyHousehold()
    })
    await page.goto('/profile/address/info')

    await expect(
      page.getByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
    ).toBeVisible()
    await expect(
      page.getByRole('link', { name: /learn how to get a replacement snap or tanf ebt card/i })
    ).toHaveAttribute('href', '/cards/info')
  })

  test('cards info page explains SNAP/TANF replacement and hides sun-bucks dashboard alert', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      isCoLoaded: true,
      householdData: makeCoLoadedOnlyHousehold({ applications: [] })
    })
    await page.goto('/cards/info')

    await expect(
      page.getByRole('heading', { name: /getting a replacement snap or tanf ebt card/i })
    ).toBeVisible()
    // Fully co-loaded households have no Summer Ebt card to act on — suppress the alert.
    await expect(page.getByRole('link', { name: /go to the dashboard/i })).toHaveCount(0)
  })

  test('cards info page shows sun-bucks dashboard alert when a Summer Ebt case remains', async ({
    page
  }) => {
    await setupApiRoutes(page, {
      isCoLoaded: true,
      householdData: makeHouseholdData({
        benefitIssuanceType: 1,
        coLoadedCohort: 2, // MixedOrApplicantExcluded
        summerEbtCases: [
          makeSummerEbtCase({ issuanceType: 1, childFirstName: 'Aiden', childLastName: 'Chen' }),
          makeSummerEbtCase({
            summerEBTCaseID: 'SNAP-MIXED-001',
            issuanceType: 3,
            childFirstName: 'Lily',
            childLastName: 'Chen',
            allowCardReplacement: false
          })
        ]
      })
    })
    await page.goto('/cards/info')

    await expect(page.getByRole('link', { name: /go to the dashboard/i })).toBeVisible()
  })
})
