import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'

test.describe('DashboardAlerts', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page)
  })

  test('shows only the benefit expiration banner on plain dashboard', async ({ page }) => {
    await page.goto('/dashboard')
    // Anchor on rendered household data first: the count assertion would
    // otherwise run while the dashboard is still fetching.
    await expect(page.getByRole('button', { name: 'John Doe' })).toBeVisible()
    // Page-level alerts render via the design-system Alert (usa-alert class
    // AND role="alert"). The card-status badge reuses usa-alert classes for
    // styling but is not an alert, and the Next.js route announcer has the
    // role but not the class; both are correctly excluded here. With
    // enable_apply off (the fixture default), the only match is the
    // persistent benefit expiration banner, with no flash alerts.
    const alerts = page.locator('.usa-alert[role="alert"]')
    await expect(alerts).toHaveCount(1)
    await expect(alerts).toHaveClass(/usa-alert--warning/)
    // Substring shared by the DC and CO English banner titles.
    await expect(alerts).toContainText('expire 122 days after issuance')
  })

  test('shows address updated alert on ?addressUpdated=true', async ({ page }) => {
    await page.goto('/dashboard?addressUpdated=true')
    await expect(
      page.locator('.usa-alert--success', { hasText: 'Your mailing address has been updated' })
    ).toBeVisible()
  })

  test('shows contact preferences updated alert on ?contactUpdated=true', async ({ page }) => {
    await page.goto('/dashboard?contactUpdated=true')
    await expect(
      page.locator('.usa-alert--success', {
        hasText: 'Your contact preferences have been updated'
      })
    ).toBeVisible()
  })

  test('shows card replaced success alert on ?flash=card_replaced', async ({ page }) => {
    await page.goto('/dashboard?flash=card_replaced')
    await expect(
      page.locator('.usa-alert--success', {
        hasText: 'New cards usually arrive in your mailbox'
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
        hasText: 'New cards usually arrive in your mailbox'
      })
    ).toBeVisible()
    // ...but the param has been removed from the URL
    await expect(page).not.toHaveURL(/flash=card_replaced/)
  })
})

// Sibling describe: setupApiRoutes is registered per test here, so the
// feature-flag override is not stacked on the beforeEach registration above.
test.describe('DashboardAlerts with applications open', () => {
  test.beforeEach(async ({ page }) => {
    await injectAuth(page)
  })

  test('shows no alert on plain dashboard when enable_apply is on', async ({ page }) => {
    await setupApiRoutes(page, { featureFlags: { enable_apply: true } })
    await page.goto('/dashboard')
    // Anchor on rendered household data first: toHaveCount(0) would otherwise
    // pass vacuously while the dashboard is still fetching.
    await expect(page.getByRole('button', { name: 'John Doe' })).toBeVisible()
    // The benefit expiration banner only renders while applications are
    // closed, so with enable_apply on the dashboard has no alerts at all.
    await expect(page.locator('.usa-alert[role="alert"]')).toHaveCount(0)
  })
})
