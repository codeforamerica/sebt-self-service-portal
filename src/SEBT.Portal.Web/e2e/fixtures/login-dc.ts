import { expect, type Page } from '@playwright/test'

import { clearMailpitMessages, waitForOtpEmail } from './mailpit'

/**
 * Completes the DC email OTP login flow against a live backend.
 * Clears Mailpit first, submits the login form, reads the OTP from Mailpit, and verifies.
 */
export async function loginWithEmailOtp(page: Page, email: string): Promise<void> {
  await clearMailpitMessages()

  await page.goto('/login')
  await page.locator('[name="email"]').fill(email)
  await page.getByRole('button', { name: /^continue$/i }).click()

  await expect(page).toHaveURL(/\/login\/verify\/?$/)
  await expect(page.locator('[name="otp"]')).toBeVisible()

  const otp = await waitForOtpEmail(email)
  await page.locator('[name="otp"]').fill(otp)
  await page.getByRole('button', { name: /^confirm$/i }).click()
}
