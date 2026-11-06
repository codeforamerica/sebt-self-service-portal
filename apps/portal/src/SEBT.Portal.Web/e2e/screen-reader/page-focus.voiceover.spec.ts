import { voiceOverTest as test } from '@guidepup/playwright'
import { expect } from '@playwright/test'

import { skipUnlessLoginMode } from '../fixtures/state'

// What a screen reader user hears on client-side navigation: VoiceOver must
// speak the destination's level-1 heading because focus moved to it.
const SIGNED_OUT = { status: 401, contentType: 'application/json', body: '{}' }
const FEATURE_FLAGS = { outage_page_enabled: false, enable_beta_banner: false }

function spokeFocusedHeading(log: string[], headingText: string): boolean {
  return log.some((phrase) => phrase.includes('heading level 1') && phrase.includes(headingText))
}

test.describe('VoiceOver on client-side navigation', () => {
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

  test('continuing from login to verify speaks the new page heading', async ({
    page,
    voiceOver
  }) => {
    skipUnlessLoginMode('otp')

    await page.goto('/login')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await voiceOver.navigateToWebContent()

    await page.getByRole('textbox', { name: /email/i }).fill('focus-check@example.com')
    await voiceOver.clearSpokenPhraseLog()
    await page.getByRole('button', { name: /^continue$/i }).click()
    await expect(page).toHaveURL(/\/login\/verify\/?$/)

    const heading = page.getByRole('heading', { level: 1 })
    await expect(heading).toBeFocused()
    const headingText = (await heading.textContent())?.trim() ?? ''
    expect(headingText).not.toBe('')

    await expect
      .poll(async () => spokeFocusedHeading(await voiceOver.spokenPhraseLog(), headingText), {
        message: `VoiceOver did not speak "${headingText}" as a level 1 heading`
      })
      .toBe(true)
  })
})
