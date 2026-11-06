import { voiceOverTest as test } from '@guidepup/playwright'
import { expect } from '@playwright/test'

// What a screen reader user hears on client-side navigation: VoiceOver must
// speak the destination's level-1 heading because focus moved to it.
function spokeFocusedHeading(log: string[], headingText: string): boolean {
  return log.some((phrase) => phrase.includes('heading level 1') && phrase.includes(headingText))
}

test.describe('VoiceOver on client-side navigation', () => {
  test('landing to disclaimer speaks the new page heading', async ({ page, voiceOver }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await voiceOver.navigateToWebContent()

    await voiceOver.clearSpokenPhraseLog()
    await page.getByRole('button', { name: /apply now/i }).click()
    await page.waitForURL('**/disclaimer')

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
