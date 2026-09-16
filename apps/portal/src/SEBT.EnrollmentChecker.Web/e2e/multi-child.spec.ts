import { expect, test } from '@playwright/test'

import { fillChildForm, startCheckFlow } from './fixtures'

test.describe('Multi-child household', () => {
  test('add, edit, and remove children from the review page', async ({ page }) => {
    await startCheckFlow(page)

    // First child → review
    await fillChildForm(page, { firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/review')
    await expect(page.getByText(/Jane Doe/)).toBeVisible()

    // Add a second child
    await page.getByRole('button', { name: /add another child/i }).click()
    await page.waitForURL('**/check')
    await fillChildForm(page, { firstName: 'Alex', lastName: 'Doe', month: '9', day: '2', year: '2017' })
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/review')
    await expect(page.getByText(/Jane Doe/)).toBeVisible()
    await expect(page.getByText(/Alex Doe/)).toBeVisible()

    // Edit the first child: the form comes back pre-filled, and the change
    // shows on review afterwards.
    await page
      .getByRole('button', { name: /update this child's information: Jane/i })
      .click()
    await page.waitForURL('**/check')
    await expect(page.getByRole('textbox', { name: /first name/i })).toHaveValue('Jane')
    await page.getByRole('textbox', { name: /first name/i }).fill('Janet')
    await page.getByRole('button', { name: /continue/i }).click()
    await page.waitForURL('**/review')
    await expect(page.getByText(/Janet Doe/)).toBeVisible()
    await expect(page.getByText(/Jane Doe/)).toHaveCount(0)

    // Remove the second child
    await page.getByRole('button', { name: /remove: Alex/i }).click()
    await expect(page.getByText(/Alex Doe/)).toHaveCount(0)
    await expect(page.getByText(/Janet Doe/)).toBeVisible()

    // Removing the last child disables submit — there is nothing to check.
    await page.getByRole('button', { name: /remove: Janet/i }).click()
    await expect(page.getByRole('button', { name: /submit/i })).toBeDisabled()
  })

  test('required-field validation blocks an empty child form', async ({ page }) => {
    await startCheckFlow(page)

    await page.getByRole('button', { name: /continue/i }).click()

    // Stays on the form with an error per required field.
    await expect(page).toHaveURL(/\/check/)
    await expect(page.getByText("Enter child's first name")).toBeVisible()
    await expect(page.getByText("Enter child's last name")).toBeVisible()
    await expect(page.getByText('Select a month')).toBeVisible()
    await expect(page.getByText('Provide a day using one or two numbers')).toBeVisible()
    await expect(page.getByText('Provide a year using four numbers')).toBeVisible()
  })

  test('rejects an impossible calendar date', async ({ page }) => {
    await startCheckFlow(page)

    await fillChildForm(page, { firstName: 'Jane', lastName: 'Doe', month: '2', day: '30', year: '2015' })
    await page.getByRole('button', { name: /continue/i }).click()

    await expect(page).toHaveURL(/\/check/)
    await expect(page.getByText(/Enter a valid birth date within the last 100 years/)).toBeVisible()
  })
})
