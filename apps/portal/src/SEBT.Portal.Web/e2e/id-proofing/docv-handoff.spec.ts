import { expect, test } from '@playwright/test'

import { setupApiRoutes } from '../fixtures/api-routes'
import { injectAuth } from '../fixtures/auth'
import { skipUnlessState } from '../fixtures/state'

// The real Socure document-verification flow can't run in CI; this covers the
// portal's side of the hand-off — a submission that needs document verification
// routes to the doc-verify page carrying the challenge id.
test.describe('Identity proofing document-verification hand-off', () => {
  test.beforeEach(() => skipUnlessState('dc'))

  test('a docv-required result routes to the document verification page', async ({ page }) => {
    await injectAuth(page)
    await setupApiRoutes(page)
    await page.route('**/api/id-proofing*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ result: 'documentVerificationRequired', challengeId: 'ch-123' })
      })
    )

    await page.goto('/login/id-proofing')

    // Adult date of birth + "none of the above" for the optional ID.
    await page.getByLabel(/month/i).first().selectOption('01')
    await page.getByRole('textbox', { name: /day/i }).fill('15')
    await page.getByRole('textbox', { name: /year/i }).fill('1990')
    // USWDS tile radios visually hide the input; click the label text instead.
    await page.getByText('None of the above', { exact: true }).click()
    await page.getByRole('button', { name: /continue/i }).click()

    await page.waitForURL('**/login/id-proofing/doc-verify?challengeId=ch-123')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })
})
