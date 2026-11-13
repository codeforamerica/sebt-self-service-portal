import { expect, test, type Locator, type Page } from '@playwright/test'

import {
  completeCheckFlow,
  fillChildForm,
  makeResult,
  mockCheckResults,
  mockFeatures,
  startCheckFlow
} from './fixtures'

// The designs put every screen's content in a 480px column centered in the
// 1024px page frame. The h1 is a block element, so its box is the column's box.
const COLUMN_MAX_WIDTH = 480

test.use({ viewport: { width: 1419, height: 858 } })

async function expectContentColumn(
  page: Page,
  content: Locator = page.getByRole('heading', { level: 1 }).first()
): Promise<void> {
  await expect(content).toBeVisible()

  const box = await content.boundingBox()
  expect(box, 'content has a bounding box').not.toBeNull()
  const { x, width } = box!

  // Measure against the layout width, not the viewport: a scrollbar narrows it.
  const layoutWidth = await page.evaluate(() => document.documentElement.clientWidth)
  const expectedLeft = (layoutWidth - width) / 2

  expect(width, `column width ${width}px`).toBeLessThanOrEqual(COLUMN_MAX_WIDTH)
  expect(
    Math.abs(x - expectedLeft),
    `column left ${x}px, centered would be ${expectedLeft}px`
  ).toBeLessThanOrEqual(2)
}

test.describe('Content column', () => {
  test.beforeEach(async ({ page }) => {
    await mockFeatures(page)
  })

  test('landing page', async ({ page }) => {
    await page.goto('/')
    await expectContentColumn(page)
  })

  test('disclaimer page', async ({ page }) => {
    await page.goto('/disclaimer')
    await expectContentColumn(page)
  })

  test('child form page', async ({ page }) => {
    await page.goto('/check')
    await expectContentColumn(page)
  })

  test('review page', async ({ page }) => {
    await startCheckFlow(page)
    await fillChildForm(page, {
      firstName: 'Jane',
      lastName: 'Doe',
      month: '4',
      day: '12',
      year: '2015'
    })
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/review')
    await expectContentColumn(page)
  })

  test('results page', async ({ page }) => {
    await mockCheckResults(page, [makeResult()])
    await completeCheckFlow(page)
    await expectContentColumn(page)
  })

  test('rate-limit alert', async ({ page }) => {
    await page.route('**/api/enrollment/check', (route) => route.fulfill({ status: 429 }))
    await startCheckFlow(page)
    await fillChildForm(page, {
      firstName: 'Jane',
      lastName: 'Doe',
      month: '4',
      day: '12',
      year: '2015'
    })
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/review')
    await page.getByRole('button', { name: /submit/i }).click()
    // Scoped to main: Next's route announcer is also role="alert".
    await expectContentColumn(page, page.getByRole('main').getByRole('alert'))
  })

  test('closed-season landing page', async ({ page }) => {
    await page.goto('/closed')
    await expectContentColumn(page)
  })

  test('not-found page', async ({ page }) => {
    await page.goto('/this-page-does-not-exist')
    await expectContentColumn(page)
  })

  test('outage page', async ({ page }) => {
    await mockFeatures(page, { outage: true })
    await page.goto('/')
    await page.waitForURL('**/outage')
    await expectContentColumn(page)
  })
})
