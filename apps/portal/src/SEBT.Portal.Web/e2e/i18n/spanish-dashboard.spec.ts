import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { makeHouseholdData } from '../fixtures/household-data'

// Spanish coverage on an authenticated page (the login screen is covered
// elsewhere). The language choice persists the way the app persists it —
// the i18next localStorage key — so this exercises the same path a returning
// Spanish-language user takes. The action label is identical for DC and CO.
const ES_CHANGE_ADDRESS = 'Cambiar mi dirección de correspondencia'

test.describe('Spanish on the authenticated dashboard', () => {
  test('dashboard quick actions render in Spanish', async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page, { householdData: makeHouseholdData() })
    await page.addInitScript(() => localStorage.setItem('i18nextLng', 'es'))

    await page.goto('/dashboard')

    await expect(page.getByRole('link', { name: ES_CHANGE_ADDRESS }).first()).toBeVisible()
    await expect(page.locator('html')).toHaveAttribute('lang', 'es')
  })
})
