import { expect, test } from '@playwright/test'

import { completeCheckFlow, fillChildForm, makeResult, mockCheckResults, startCheckFlow } from './fixtures'

test.describe('Enrollment checker happy path', () => {
  test('navigates from landing to enrolled results', async ({ page }) => {
    await mockCheckResults(page, [makeResult({ status: 'Match' })])

    await completeCheckFlow(page)

    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible()
    // The enrolled child appears in the results summary with a portal link to
    // manage their benefits.
    await expect(page.getByText(/Jane/)).toBeVisible()
    await expect(page.getByTestId('portal-link')).toBeVisible()
  })

  test('review page shows the entered child before submit', async ({ page }) => {
    await mockCheckResults(page, [makeResult()])

    await startCheckFlow(page)
    await fillChildForm(page, { firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
    await page.getByRole('button', { name: /continue/i }).click()

    await page.waitForURL('**/review')
    await expect(page.getByText(/Jane Doe/i)).toBeVisible()
    await expect(page.getByText(/April 12, 2015/i)).toBeVisible()
  })

  test('back button returns from disclaimer to landing', async ({ page }) => {
    await page.goto('/disclaimer')
    await page.getByRole('button', { name: /back/i }).click()
    await expect(page).toHaveURL(/\/$/)
  })

  test('/closed page renders', async ({ page }) => {
    await page.goto('/closed')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })
})
