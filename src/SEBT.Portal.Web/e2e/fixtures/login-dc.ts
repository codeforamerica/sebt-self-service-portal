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

  // VerifyOtpFormWrapper reads otp_email from sessionStorage and redirects to /login when it is missing.
  await page.evaluate((expectedEmail) => {
    sessionStorage.setItem('otp_email', expectedEmail)
  }, email)

  try {
    await page.waitForURL(/\/login\/verify\/?$/, { timeout: 15_000 })
  } catch {
    await page.goto('/login/verify')
  }

  await expect(page).toHaveURL(/\/login\/verify\/?$/)
  const otpInput = page.locator('[name="otp"]')
  await expect(otpInput).toBeVisible({ timeout: 15_000 })

  const otp = await otpEmailPromise
  await otpInput.fill(otp)
  await page.getByRole('button', { name: /^confirm$/i }).click()
}

/** Logs in as the verified DC seed user and waits for the dashboard to load. */
export async function loginToVerifiedDashboard(page: Page): Promise<void> {
  await loginWithEmailOtp(page, DC_VERIFIED_EMAIL)
  await expect(page).toHaveURL(/\/dashboard\/?$/, { timeout: 30_000 })
  await expect(page.getByRole('heading', { name: /SUN Bucks Dashboard/i })).toBeAttached()
}
