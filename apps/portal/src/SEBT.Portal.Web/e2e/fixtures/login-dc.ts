import { expect, type Page } from '@playwright/test'

import { clearMailpitMessages, waitForOtpEmail } from './mailpit'
import { DC_VERIFIED_EMAIL } from './seed-users'

/**
 * Submits the login form and waits for the OTP verify screen.
 * Returns a Mailpit poll promise when the caller will read the emailed code.
 */
export async function reachOtpVerifyPage(
  page: Page,
  email: string,
  options: { waitForEmail?: boolean } = {}
): Promise<{ otpEmailPromise?: Promise<string> }> {
  const waitForEmail = options.waitForEmail ?? true

  await clearMailpitMessages()

  await page.goto('/login')
  const emailInput = page.getByRole('textbox', { name: /email/i })
  await expect(emailInput).toBeVisible()
  await emailInput.fill(email)
  await expect(emailInput).toHaveValue(email)

  const otpRequestResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/auth/otp/request') && response.request().method() === 'POST'
  )

  await page.getByRole('button', { name: /^continue$/i }).click()
  const otpRequest = await otpRequestResponse
  expect(
    otpRequest.ok(),
    `OTP request failed with status ${otpRequest.status()} for ${email}`
  ).toBeTruthy()

  // LoginForm sets otp_email in sessionStorage then router.push('/login/verify').
  await expect(page).toHaveURL(/\/login\/verify\/?$/, { timeout: 15_000 })
  await expect
    .poll(async () => page.evaluate(() => sessionStorage.getItem('otp_email')))
    .toBe(email)
  await expect(page.locator('[name="otp"]')).toBeVisible({ timeout: 15_000 })

  // Poll Mailpit only after the verify screen is ready so a failed assertion above
  // does not leave a background poll running until timeout.
  if (waitForEmail) {
    return { otpEmailPromise: waitForOtpEmail(email) }
  }

  return {}
}

/** Submits an OTP code on /login/verify and waits for the validate API response. */
export async function submitOtpOnVerifyPage(page: Page, otp: string) {
  const validateResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/auth/otp/validate') && response.request().method() === 'POST'
  )

  await page.locator('[name="otp"]').fill(otp)
  await page.getByRole('button', { name: /^confirm$/i }).click()

  return await validateResponse
}

/**
 * Completes the DC email OTP login flow against a live backend.
 * Clears Mailpit first, submits the login form, reads the OTP from Mailpit, and verifies.
 */
export async function loginWithEmailOtp(page: Page, email: string): Promise<void> {
  const { otpEmailPromise } = await reachOtpVerifyPage(page, email)
  const otp = await otpEmailPromise!
  await submitOtpOnVerifyPage(page, otp)
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
