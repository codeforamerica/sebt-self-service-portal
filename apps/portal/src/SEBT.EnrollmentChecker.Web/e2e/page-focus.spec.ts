import { expect, type Page, test } from '@playwright/test'

// Client-side navigation must orient a screen reader user: the destination's
// level-1 heading takes focus and the document title changes to match it.
async function activeElementTag(page: Page): Promise<string | undefined> {
  return page.evaluate(() => document.activeElement?.tagName)
}

async function expectFocusedAndTitled(page: Page): Promise<void> {
  const heading = page.getByRole('heading', { level: 1 })
  await expect(heading).toBeVisible()
  const headingText = (await heading.textContent())?.trim() ?? ''
  expect(headingText).not.toBe('')

  await expect(heading).toBeFocused()
  await expect.poll(() => page.title()).toContain(`${headingText} | `)
}

test.describe('Page focus on client-side navigation', () => {
  test('a direct load leaves focus on the document body', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    expect(await activeElementTag(page)).toBe('BODY')
  })

  test('landing to disclaimer focuses the new heading and retitles the document', async ({
    page
  }) => {
    await page.goto('/')
    await page.getByRole('button', { name: /apply now/i }).click()
    await page.waitForURL('**/disclaimer')

    await expectFocusedAndTitled(page)
  })

  test('disclaimer to check focuses the new heading and retitles the document', async ({
    page
  }) => {
    await page.goto('/disclaimer')
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/check')

    await expectFocusedAndTitled(page)
  })
})
