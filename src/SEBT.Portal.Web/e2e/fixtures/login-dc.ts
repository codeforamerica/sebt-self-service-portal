import { expect, type Page } from '@playwright/test'

import { clearMailpitMessages, waitForOtpEmail } from './mailpit'
import { DC_VERIFIED_EMAIL } from './seed-users'

/**
 * Completes the DC email OTP login flow against a live backend.
 * Clears Mailpit first, submits the login form, reads the OTP from Mailpit, and verifies.
 */
export async function loginWithEmailOtp(page: Page, email: string): Promise<void> {
  await clearMailpitMessages()

  await page.goto('/login')
  await page.locator('[name="email"]').fill(email)

  const otpRequestResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/auth/otp/request') &&
      response.request().method() === 'POST' &&
      response.ok()
  )
  const otpEmailPromise = waitForOtpEmail(email)

  await page.getByRole('button', { name: /^continue$/i }).click()
  await otpRequestResponse

  // LoginForm sets otp_email in sessionStorage then router.push('/login/verify').
  await expect(page).toHaveURL(/\/login\/verify\/?$/, { timeout: 15_000 })
  await expect
    .poll(async () => page.evaluate(() => sessionStorage.getItem('otp_email')))
    .toBe(email)
  const otpInput = page.locator('[name="otp"]')
  await expect(otpInput).toBeVisible({ timeout: 15_000 })

  const otp = await otpEmailPromise
  await otpInput.fill(otp)
  await page.getByRole('button', { name: /^confirm$/i }).click()
}

/** Logs in with OTP and waits for the browser to land on the expected post-login URL. */
export async function loginWithEmailOtpExpecting(
  page: Page,
  email: string,
  landingUrl: RegExp
): Promise<void> {
  await loginWithEmailOtp(page, email)
  await expect(page).toHaveURL(landingUrl, { timeout: 30_000 })
}

/** Logs in as the verified DC seed user and waits for the dashboard to load. */
export async function loginToVerifiedDashboard(page: Page): Promise<void> {
  await loginWithEmailOtpExpecting(page, DC_VERIFIED_EMAIL, /\/dashboard\/?$/)
  await expect(page.getByRole('heading', { name: /SUN Bucks Dashboard/i })).toBeAttached()
}
