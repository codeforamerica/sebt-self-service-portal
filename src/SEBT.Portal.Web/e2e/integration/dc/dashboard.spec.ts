import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginToVerifiedDashboard } from '../../fixtures/login-dc'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC dashboard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test.describe('after OTP login', () => {
    test.beforeEach(async ({ page }) => {
      await loginToVerifiedDashboard(page)
    })

    test('shows the household dashboard with enrolled children', async ({ page }) => {
      // MockHouseholdRepository "verified" scenario — John and Jane Doe enrolled cases.
      await expect(page.getByText('John', { exact: false }).first()).toBeVisible()
      await expect(page.getByText('Jane', { exact: false }).first()).toBeVisible()
    })

    test('shows guardian profile and household summary', async ({ page }) => {
      await expect(page.getByRole('heading', { name: /John R\. DoeMOCK/i })).toBeVisible()
      await expect(page.getByText('Enrolled', { exact: true })).toBeVisible()
      await expect(page.getByText('123 Main Street')).toBeVisible()
      await expect(page.getByText('Denver', { exact: false })).toBeVisible()
    })

    test('shows navigation to change mailing address', async ({ page }) => {
      const changeAddressLink = page.getByRole('link', { name: /change.*mailing address/i })
      await expect(changeAddressLink.first()).toBeVisible()
      await expect(changeAddressLink.first()).toHaveAttribute('href', '/profile/address')
    })

    test('shows logout link on the dashboard', async ({ page }) => {
      const logoutLink = page.getByRole('link', { name: /log out|logout/i })
      await expect(logoutLink).toBeVisible()
      await expect(logoutLink).toHaveAttribute('href', '/api/auth/logout')
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
