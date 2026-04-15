import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'

test.describe('DashboardAlerts', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page)
  })

  test('shows no alert on plain dashboard', async ({ page }) => {
    await page.goto('/dashboard')
    await expect(page.locator('.usa-alert')).toHaveCount(0)
  })

  test('shows address updated alert on ?addressUpdated=true', async ({ page }) => {
    await page.goto('/dashboard?addressUpdated=true')
    await expect(
      page.locator('.usa-alert--success', { hasText: 'Address update recorded' })
    ).toBeVisible()
  })

  test('shows address + card replacement alert on ?addressUpdated=true&cardsRequested=true', async ({
    page
  }) => {
    await page.goto('/dashboard?addressUpdated=true&cardsRequested=true')
    await expect(
      page.locator('.usa-alert--success', {
        hasText: 'Address update and card replacement recorded'
      })
    ).toBeVisible()
  })

  test('shows card replaced success alert on ?flash=card_replaced', async ({ page }) => {
    await page.goto('/dashboard?flash=card_replaced')
    await expect(
      page.locator('.usa-alert--success', {
        hasText: 'Your replacement card request has been recorded'
      })
    ).toBeVisible()
  })

  test('shows address update failed warning on ?addressUpdateFailed=true', async ({ page }) => {
    await page.goto('/dashboard?addressUpdateFailed=true')
    await expect(
      page.locator('.usa-alert--warning', {
        hasText: 'There was an issue updating your mailing address.'
      })
    ).toBeVisible()
  })

  test('shows contact update failed warning on ?contactUpdateFailed=true', async ({ page }) => {
    await page.goto('/dashboard?contactUpdateFailed=true')
    await expect(
      page.locator('.usa-alert--warning', {
        hasText: 'There was an issue updating your contact preferences.'
      })
    ).toBeVisible()
  })

  test('shows address verification warning on ?addressVerification=true', async ({ page }) => {
    await page.goto('/dashboard?addressVerification=true')
    await expect(
      page.locator('.usa-alert--warning', { hasText: 'Is your address correct?' })
    ).toBeVisible()
  })

  test('alert URL params are cleaned from the URL after display', async ({ page }) => {
    await page.goto('/dashboard?flash=card_replaced')
    // Alert is visible...
    await expect(
      page.locator('.usa-alert--success', {
        hasText: 'Your replacement card request has been recorded'
      })
    ).toBeVisible()
    // ...but the param has been removed from the URL
    await expect(page).not.toHaveURL(/flash=card_replaced/)
  })
})
