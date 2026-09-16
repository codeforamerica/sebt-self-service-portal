import { expect, test } from '@playwright/test'

import { mockFeatures } from './fixtures'

// The outage page rides the features poll: while outagePage.enabled is true,
// every checker route is replaced by /outage (and /outage bounces back to the
// landing page once the outage clears).
test.describe('Outage page', () => {
  test('active outage replaces the check flow with the outage page', async ({ page }) => {
    await mockFeatures(page, { outage: true })

    await page.goto('/')

    await page.waitForURL('**/outage')
    await expect(page.getByRole('heading', { name: /try again later/i })).toBeVisible()
  })

  test('active outage covers deep links into the flow', async ({ page }) => {
    await mockFeatures(page, { outage: true })

    await page.goto('/check')

    await page.waitForURL('**/outage')
    await expect(page.getByRole('heading', { name: /try again later/i })).toBeVisible()
  })

  test('without an outage the landing page renders and /outage bounces home', async ({ page }) => {
    await mockFeatures(page, { outage: false })

    await page.goto('/')
    await expect(page.getByRole('button', { name: /check enrollment/i })).toBeVisible()

    await page.goto('/outage')
    await page.waitForURL(/\/$/)
  })
})
