import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import {
  loginWithEmailOtpExpecting,
  reachOtpVerifyPage,
  submitOtpOnVerifyPage
} from '../../fixtures/login-dc'
import { DC_ID_PROOF_IN_PROGRESS_EMAIL, DC_VERIFIED_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC auth routing (full stack)', () => {
  test.describe.configure({ mode: 'serial', timeout: 60_000 })

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

  test('shows error and stays on verify when OTP is wrong', async ({ page }) => {
    await reachOtpVerifyPage(page, DC_ID_PROOF_IN_PROGRESS_EMAIL, { waitForEmail: false })

    const validateResponse = await submitOtpOnVerifyPage(page, '000000')
    expect(validateResponse.status()).toBe(400)

    await expect(page).toHaveURL(/\/login\/verify\/?$/)
    await expect(page.locator('.usa-alert--error')).toBeVisible()
    await expect(page.getByRole('heading', { name: /SUN Bucks Dashboard/i })).toHaveCount(0)
    await expect
      .poll(async () => page.evaluate(() => sessionStorage.getItem('otp_email')))
      .toBe(DC_ID_PROOF_IN_PROGRESS_EMAIL)
  })

  test('verify page redirects to login when otp_email is missing from sessionStorage', async ({
    page
  }) => {
    await page.goto('/login')
    await page.evaluate(() => sessionStorage.clear())
    await page.goto('/login/verify')
    await expect(page).toHaveURL(/\/login\/?$/, { timeout: 15_000 })
    await expect(page.locator('[name="email"]')).toBeVisible()
  })
})
