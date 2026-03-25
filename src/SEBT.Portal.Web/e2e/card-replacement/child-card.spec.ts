import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import {
  makeApplication,
  makeHouseholdData,
  OLD_CARD_DATE,
  recentCardDate
} from '../fixtures/household-data'

test.describe('ChildCard', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
  })

  test.describe('issuance type labels', () => {
    test('SummerEbt (issuanceType 1) shows a card type label under "Benefit issued to"', async ({
      page
    }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [makeApplication({ issuanceType: 1 })]
        })
      })
      await page.goto('/dashboard')
      // The card type heading is "Benefit issued to" in both DC and CO locales.
      // The value text differs by state (e.g. "DC SUN Bucks Card" vs "Summer EBT Card")
      // so we assert on the heading rather than the translated value.
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText(
        'Benefit issued to'
      )
    })

    test('SnapEbtCard (issuanceType 3) shows SNAP card type label', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({
              issuanceType: 3,
              applicationNumber: 'APP-SNAP-001',
              cardRequestedAt: OLD_CARD_DATE
            })
          ]
        })
      })
      await page.goto('/dashboard')
      // "SNAP" appears in both DC ("Household SNAP EBT Card") and CO ("SNAP EBT Card")
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText('SNAP')
    })

    test('TanfEbtCard (issuanceType 2) shows TANF card type label', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({
              issuanceType: 2,
              applicationNumber: 'APP-TANF-001',
              cardRequestedAt: OLD_CARD_DATE
            })
          ]
        })
      })
      await page.goto('/dashboard')
      // "TANF" appears in both DC ("Household TANF EBT Card") and CO ("Colorado Works (TANF) EBT Card")
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText('TANF')
    })
  })

  test.describe('feature flags', () => {
    test('show_case_number=true shows SEBT ID row', async ({ page }) => {
      await setupApiRoutes(page, {
        featureFlags: { show_case_number: true }
      })
      await page.goto('/dashboard')
      // The ChildCard renders the case number when the flag is on
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText('CASE-100001')
    })

    test('show_case_number=false hides SEBT ID row', async ({ page }) => {
      await setupApiRoutes(page, {
        featureFlags: { show_case_number: false }
      })
      await page.goto('/dashboard')
      await expect(page.locator('[data-testid="accordion-content"]')).not.toContainText(
        'CASE-100001'
      )
    })

    test('show_card_last4=true shows card number row', async ({ page }) => {
      await setupApiRoutes(page, {
        featureFlags: { show_card_last4: true }
      })
      await page.goto('/dashboard')
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText('1234')
    })

    test('show_card_last4=false hides card number row', async ({ page }) => {
      await setupApiRoutes(page, {
        featureFlags: { show_card_last4: false }
      })
      await page.goto('/dashboard')
      await expect(page.locator('[data-testid="accordion-content"]')).not.toContainText('1234')
    })
  })

  test.describe('replacement link visibility', () => {
    test('shows replacement link when card was requested more than 14 days ago', async ({
      page
    }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [makeApplication({ cardRequestedAt: OLD_CARD_DATE, issuanceType: 1 })]
        })
      })
      await page.goto('/dashboard')
      await expect(
        page.locator('[data-testid="accordion-content"] a', {
          hasText: 'Request a replacement card'
        })
      ).toBeVisible()
    })

    test('hides replacement link when card was requested within the last 14 days (cooldown)', async ({
      page
    }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [makeApplication({ cardRequestedAt: recentCardDate(), issuanceType: 1 })]
        })
      })
      await page.goto('/dashboard')
      await expect(
        page.locator('[data-testid="accordion-content"] a', {
          hasText: 'Request a replacement card'
        })
      ).not.toBeVisible()
    })

    test('replacement link points to /cards/replace for SummerEbt', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({
              applicationNumber: 'APP-2026-001',
              cardRequestedAt: OLD_CARD_DATE,
              issuanceType: 1
            })
          ]
        })
      })
      await page.goto('/dashboard')
      const link = page.locator('[data-testid="accordion-content"] a', {
        hasText: 'Request a replacement card'
      })
      await expect(link).toHaveAttribute('href', /\/cards\/replace\?app=APP-2026-001/)
    })

    test('SnapEbtCard co-loaded: DC shows /cards/info link, CO shows no link', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({
              issuanceType: 3,
              cardRequestedAt: OLD_CARD_DATE
            })
          ]
        })
      })
      await page.goto('/dashboard')

      // NEXT_PUBLIC_STATE is inlined at build time. Detect the active state by
      // checking the page title, which is state-specific (DC says "DC SUN Bucks",
      // CO says "SUN Bucks"). The co-loaded link behavior differs by state.
      // Page title: "District of Columbia SUN Bucks ..." for DC, "Colorado SUN Bucks ..." for CO.
      const isDC = (await page.title()).includes('District of Columbia')

      const link = page.locator('[data-testid="accordion-content"] a', {
        hasText: 'Request a replacement card'
      })

      if (isDC) {
        await expect(link).toHaveAttribute('href', '/cards/info')
      } else {
        // CO: co-loaded cards produce no replacement link
        await expect(link).toHaveCount(0)
      }
    })

    test('TanfEbtCard co-loaded: DC shows /cards/info link, CO shows no link', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          applications: [
            makeApplication({
              issuanceType: 2,
              cardRequestedAt: OLD_CARD_DATE
            })
          ]
        })
      })
      await page.goto('/dashboard')

      // Page title: "District of Columbia SUN Bucks ..." for DC, "Colorado SUN Bucks ..." for CO.
      const isDC = (await page.title()).includes('District of Columbia')

      const link = page.locator('[data-testid="accordion-content"] a', {
        hasText: 'Request a replacement card'
      })

      if (isDC) {
        await expect(link).toHaveAttribute('href', '/cards/info')
      } else {
        await expect(link).toHaveCount(0)
      }
    })
  })
})
