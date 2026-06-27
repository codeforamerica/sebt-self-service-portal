import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import { DC_ID_PROOF_IN_PROGRESS_EMAIL, DC_VERIFIED_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC auth routing (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test('clears otp_email from sessionStorage after successful OTP login', async ({ page }) => {
    await loginWithEmailOtpExpecting(page, DC_VERIFIED_EMAIL, /\/dashboard\/?$/)

    // Successful verify clears otp_email before routing to the dashboard.
    await expect
      .poll(async () => page.evaluate(() => sessionStorage.getItem('otp_email')))
      .toBeNull()
  })

  test('id-proof-in-progress user lands on id-proofing after OTP', async ({ page }) => {
    await loginWithEmailOtpExpecting(
      page,
      DC_ID_PROOF_IN_PROGRESS_EMAIL,
      /\/login\/id-proofing\/?$/
    )

    await expect(page.locator('#id-proofing-title')).toBeVisible()
  })

  test('verify page redirects to login when otp_email is missing from sessionStorage', async ({
    page
  }) => {
    await page.goto('/login/verify')
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
    await expect(page.locator('[name="email"]')).toBeVisible()
  })
})
