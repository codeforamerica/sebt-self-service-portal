import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { expect, test } from '@playwright/test'

import { isFullStackE2E, skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginToVerifiedDashboard } from '../../fixtures/login-dc'
import { skipUnlessState } from '../../fixtures/state'

const authDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '../../.auth')
const verifiedDashboardAuthFile = path.join(authDir, 'verified-dashboard.json')
const emptyStorageState = JSON.stringify({ cookies: [], origins: [] })

test.describe('DC dashboard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test.describe('after OTP login', () => {
    test.describe.configure({ mode: 'serial' })

    // Child test.use({ storageState }) still applies to this beforeAll hook.
    // Seed an empty auth file first so browser.newContext() can load it, then overwrite after login.
    test.beforeAll(async ({ browser }) => {
      fs.mkdirSync(authDir, { recursive: true })
      fs.writeFileSync(verifiedDashboardAuthFile, emptyStorageState)

      if (!isFullStackE2E) {
        return
      }

      const context = await browser.newContext()
      const page = await context.newPage()
      await loginToVerifiedDashboard(page)
      await context.storageState({ path: verifiedDashboardAuthFile })
      await context.close()
    })

    test.describe('with saved session', () => {
      test.use({ storageState: verifiedDashboardAuthFile })

      test('shows the household dashboard with enrolled children', async ({ page }) => {
        await page.goto('/dashboard')
        // MockHouseholdRepository "verified" scenario — John and Jane Doe enrolled cases.
        await expect(page.getByText('John', { exact: false }).first()).toBeVisible()
        await expect(page.getByText('Jane', { exact: false }).first()).toBeVisible()
      })

      test('shows guardian profile and household summary', async ({ page }) => {
        await page.goto('/dashboard')
        await expect(page.getByRole('heading', { name: /John R\. DoeMOCK/i })).toBeVisible()
        await expect(page.getByText('Enrolled', { exact: true })).toBeVisible()
        await expect(page.getByText('123 Main Street')).toBeVisible()
        await expect(page.getByText('Denver', { exact: false })).toBeVisible()
      })

      test('shows co-loaded mailing address info link', async ({ page }) => {
        await page.goto('/dashboard')
        // Verified mock cases use SnapEbtCard issuance — address updates are not self-service in DC.
        const addressInfoLink = page.getByRole('link', {
          name: /how we determine your mailing address/i
        })
        await expect(addressInfoLink).toBeVisible()
        await expect(addressInfoLink).toHaveAttribute('href', '/profile/address/info')
      })

      test('shows logout link on the dashboard', async ({ page }) => {
        await page.goto('/dashboard')
        const logoutLink = page.getByRole('link', { name: /log out|logout/i })
        await expect(logoutLink).toBeVisible()
        await expect(logoutLink).toHaveAttribute('href', '/api/auth/logout')
      })
    })
  })

  test('logs out and clears the session', async ({ page }) => {
    await loginToVerifiedDashboard(page)

    await page.getByRole('link', { name: /log out|logout/i }).click()
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
    await expect(page.locator('[name="email"]')).toBeVisible()

    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
    await expect(page.locator('[name="email"]')).toBeVisible()
  })
})
