import { expect, test } from '@playwright/test'

test.describe('Enrollment checker happy path', () => {
  test('navigates from landing to results', async ({ page }) => {
    // Mock the enrollment check API so we don't need a live backend
    await page.route('**/api/enrollment/check', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [{ checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }]
        })
      })
    )

    await page.goto('/')

    // Landing page — click "Apply now"
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.getByRole('button', { name: /apply now/i }).click()

    // Disclaimer page
    await expect(page.url()).toContain('/disclaimer')
    await page.getByRole('button', { name: /continue/i }).click()

    // Check page — fill three-field birthdate
    await expect(page.url()).toContain('/check')
    await page.getByRole('textbox', { name: /first name/i }).fill('Jane')
    await page.getByRole('textbox', { name: /last name/i }).fill('Doe')
    await page.getByLabel(/month/i).selectOption('4')
    await page.getByRole('textbox', { name: /day/i }).fill('12')
    await page.getByRole('textbox', { name: /year/i }).fill('2015')
    await page.getByRole('button', { name: /continue/i }).click()

    // Review page
    await expect(page.url()).toContain('/review')
    await expect(page.getByText(/Jane Doe/i)).toBeVisible()
    await expect(page.getByText(/April, 12 2015/i)).toBeVisible()
    await page.getByRole('button', { name: /submit/i }).click()

    // Results page
    await expect(page.url()).toContain('/results')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })

  test('back button returns from disclaimer to landing', async ({ page }) => {
    await page.goto('/disclaimer')
    await page.getByRole('button', { name: /back/i }).click()
    await expect(page.url()).toMatch(/\/$/)
  })

  test('/closed page renders', async ({ page }) => {
    await page.goto('/closed')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })
})
