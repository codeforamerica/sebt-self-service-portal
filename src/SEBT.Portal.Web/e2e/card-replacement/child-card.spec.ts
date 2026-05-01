import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData, makeSummerEbtCase, recentCardDate } from '../fixtures/household-data'

test.describe('ChildCard', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
  })

  test.describe('issuance type labels', () => {
    /// applies to DC and CO
    test('SummerEbt (issuanceType 1) shows a card type label under "Benefit issued to"', async ({
      page
    }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ issuanceType: 1 })]
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

    // TODO Update with state specific text fixture
    test.skip('SnapEbtCard (issuanceType 3) shows cardTableTypeSnap label', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ issuanceType: 3 })]
        })
      })
      await page.goto('/dashboard')
      // "SNAP" appears in DC ("Household SNAP EBT Card")
      await expect(page.locator('[data-testid="accordion-content"]')).toContainText('SNAP')
    })

    // TODO Update with state specific text fixture
    test.skip('TanfEbtCard (issuanceType 2) shows cardTableTypeTanf', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ issuanceType: 2 })]
        })
      })
      await page.goto('/dashboard')
      // "TANF" appears in DC ("Household TANF EBT Card")
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

    test('when caseDisplayNumber is set it is shown as SEBT ID instead of ebtCaseNumber', async ({
      page
    }) => {
      await setupApiRoutes(page, {
        featureFlags: { show_case_number: true },
        householdData: makeHouseholdData({
          summerEbtCases: [
            makeSummerEbtCase({
              ebtCaseNumber: 'CBMS-CASE-ID',
              caseDisplayNumber: 'APP-DISPLAY-ID'
            })
          ]
        })
      })
      await page.goto('/dashboard')
      const panel = page.locator('[data-testid="accordion-content"]')
      await expect(panel).toContainText('APP-DISPLAY-ID')
      await expect(panel).not.toContainText('CBMS-CASE-ID')
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
    test('shows replacement link when card is not within cooldown', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ issuanceType: 1 })]
        })
      })
      await page.goto('/dashboard')
      // SummerEbtCase has no cardRequestedAt, so cooldown does not apply.
      // applicationId maps to applicationNumber, enabling the link.
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
          summerEbtCases: [
            makeSummerEbtCase({ issuanceType: 1, cardRequestedAt: recentCardDate() })
          ]
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
          summerEbtCases: [
            makeSummerEbtCase({
              applicationId: 'APP-2026-001',
              issuanceType: 1
            })
          ]
        })
      })
      await page.goto('/dashboard')
      const link = page.locator('[data-testid="accordion-content"] a', {
        hasText: 'Request a replacement card'
      })
      await expect(link).toHaveAttribute('href', /\/cards\/replace\?case=SEBT-001/)
    })

    test('co-loaded case (allowCardReplacement=false) shows /cards/info link', async ({ page }) => {
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ issuanceType: 3, allowCardReplacement: false })]
        })
      })
      await page.goto('/dashboard')

      const link = page.locator('[data-testid="accordion-content"] a', {
        hasText: 'Request a replacement card'
      })
      await expect(link).toHaveAttribute('href', '/cards/info')
    })
  })
})
