import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginWithEmailOtp } from '../../fixtures/login-dc'
import { DC_VERIFIED_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC dashboard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test('logs in via OTP and shows the household dashboard', async ({ page }) => {
    await loginWithEmailOtp(page, DC_VERIFIED_EMAIL)

    await expect(page).toHaveURL(/\/dashboard\/?$/, { timeout: 30_000 })
    await expect(page.getByRole('heading', { name: /SUN Bucks Dashboard/i })).toBeAttached()

    // MockHouseholdRepository "verified" scenario — John and Jane Doe enrolled cases.
    await expect(page.getByText('John', { exact: false }).first()).toBeVisible()
    await expect(page.getByText('Jane', { exact: false }).first()).toBeVisible()
  })
})
