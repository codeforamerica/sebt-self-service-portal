import { expect, type Page, test } from '@playwright/test'

import { skipUnlessLoginMode } from '../fixtures/state'

// Client-side navigation must orient a screen reader user: the destination's
// level-1 heading takes focus and the document title changes to match it.
// Next's route announcer only speaks when the title changed in the same commit
// as the route, which holds for /login/verify (no loading state).
const SIGNED_OUT = { status: 401, contentType: 'application/json', body: '{}' }
const FEATURE_FLAGS = { outage_page_enabled: false, enable_beta_banner: false }

async function routeAnnouncerText(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    const host = document.querySelector('next-route-announcer')
    return host?.shadowRoot?.querySelector('#__next-route-announcer__')?.textContent ?? null
  })
}

async function activeElementTag(page: Page): Promise<string | undefined> {
  return page.evaluate(() => document.activeElement?.tagName)
}

test.describe('Page focus on client-side navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/status*', (route) => route.fulfill(SIGNED_OUT))
    await page.route('**/api/features*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(FEATURE_FLAGS)
      })
    )
    await page.route('**/api/auth/otp/request', (route) => route.fulfill({ status: 204 }))
  })

  test('a direct load leaves focus on the document body', async ({ page }) => {
    await page.goto('/login')

    const heading = page.getByRole('heading', { level: 1 })
    await expect(heading).toBeVisible()
    await expect(heading).not.toBeFocused()
    expect(await activeElementTag(page)).toBe('BODY')
  })

  test('continuing from login to verify focuses the new heading and retitles the document', async ({
    page
  }) => {
    skipUnlessLoginMode('otp')

    await page.goto('/login')
    await page.getByRole('textbox', { name: /email/i }).fill('focus-check@example.com')

    const otpRequest = page.waitForResponse(
      (response) =>
        response.url().includes('/api/auth/otp/request') && response.request().method() === 'POST'
    )
    await page.getByRole('button', { name: /^continue$/i }).click()
    expect((await otpRequest).status()).toBe(204)
    await expect(page).toHaveURL(/\/login\/verify\/?$/)

    const heading = page.getByRole('heading', { level: 1 })
    await expect(heading).toBeVisible()
    const headingText = (await heading.textContent())?.trim() ?? ''
    expect(headingText).not.toBe('')

    await expect(heading).toBeFocused()
    await expect.poll(() => page.title()).toContain(`${headingText} | `)
    await expect.poll(() => routeAnnouncerText(page)).toBe(await page.title())
  })
})
