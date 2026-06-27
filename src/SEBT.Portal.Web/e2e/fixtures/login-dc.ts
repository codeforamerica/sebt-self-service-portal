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
  await page.getByRole('button', { name: /^continue$/i }).click()
  await otpRequestResponse

  // Production `next start` can miss client-side router.push in headless CI.
  if (!page.url().match(/\/login\/verify\/?$/)) {
    await page.goto('/login/verify')
  }

  await expect(page).toHaveURL(/\/login\/verify\/?$/)
  await expect(page.locator('[name="otp"]')).toBeVisible()

  const otp = await waitForOtpEmail(email)
  await page.locator('[name="otp"]').fill(otp)
  await page.getByRole('button', { name: /^confirm$/i }).click()
}

/** Logs in as the verified DC seed user and waits for the dashboard to load. */
export async function loginToVerifiedDashboard(page: Page): Promise<void> {
  await loginWithEmailOtp(page, DC_VERIFIED_EMAIL)
  await expect(page).toHaveURL(/\/dashboard\/?$/, { timeout: 30_000 })
  await expect(page.getByRole('heading', { name: /SUN Bucks Dashboard/i })).toBeAttached()
}
