import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData, makeSummerEbtCase } from '../fixtures/household-data'
import { skipUnlessState } from '../fixtures/state'

// Colorado-specific card lifecycle states on the dashboard child card.
// DeactivatedByState and NotActivated present as the Inactive badge; the
// remaining statuses have their own labels.
const STATUS_CASES: Array<{ apiStatus: string; badgeLabel: RegExp; description?: RegExp }> = [
  { apiStatus: 'Active', badgeLabel: /active/i, description: /set the pin/i },
  { apiStatus: 'Frozen', badgeLabel: /frozen/i, description: /while this card is frozen/i },
  {
    apiStatus: 'Undeliverable',
    badgeLabel: /undeliverable/i,
    description: /could not be delivered/i
  },
  { apiStatus: 'NotActivated', badgeLabel: /inactive/i },
  { apiStatus: 'DeactivatedByState', badgeLabel: /inactive/i }
]

test.describe('Colorado dashboard card statuses', () => {
  test.beforeEach(() => skipUnlessState('co'))

  for (const { apiStatus, badgeLabel, description } of STATUS_CASES) {
    test(`shows the ${apiStatus} card state`, async ({ page }) => {
      await injectAuth(page)
      await setupApiRoutes(page, {
        householdData: makeHouseholdData({
          summerEbtCases: [makeSummerEbtCase({ ebtCardStatus: apiStatus })]
        })
      })

      await page.goto('/dashboard')

      const badge = page.getByTestId('card-status-badge').first()
      await expect(badge).toBeVisible()
      await expect(badge).toHaveText(badgeLabel)
      if (description) {
        await expect(page.getByText(description).first()).toBeVisible()
      }
    })
  }

  test('Activate card action opens the activation page with its call-to-action', async ({
    page
  }) => {
    await injectAuth(page)
    await setupApiRoutes(page, {
      householdData: makeHouseholdData({
        summerEbtCases: [makeSummerEbtCase({ ebtCardStatus: 'NotActivated' })]
      })
    })

    await page.goto('/dashboard')

    await page.getByRole('link', { name: /activate a card/i }).click()

    await page.waitForURL('**/cards/activate')
    // The activation page's call-to-action is the tap-to-call EBT customer
    // service link.
    await expect(page.locator('a[href="tel:+18883282656"]')).toBeVisible()
  })
})
