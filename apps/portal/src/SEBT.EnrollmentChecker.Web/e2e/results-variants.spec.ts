import { expect, test } from '@playwright/test'

import { completeCheckFlow, makeResult, mockCheckResults } from './fixtures'

// Household-level result variants on /results. The API returns per-child
// statuses (Match | NonMatch | Error); the page aggregates them into
// all-enrolled / none-enrolled / mixed / indeterminate presentations.
test.describe('Results page variants', () => {
  test('all enrolled: summary box with portal link, no application steps', async ({ page }) => {
    await mockCheckResults(page, [makeResult({ status: 'Match' })])

    await completeCheckFlow(page)

    await expect(page.getByTestId('portal-link')).toBeVisible()
    await expect(page.getByTestId('next-steps')).toHaveCount(0)
    await expect(page.getByTestId('no-info-summary-box')).toHaveCount(0)
  })

  test('none enrolled: closure message with the 2027 application link', async ({ page }) => {
    await mockCheckResults(page, [makeResult({ status: 'NonMatch' })])

    await completeCheckFlow(page)

    // Closed-season copy: the closure line always shows; the application link
    // shows because the e2e server is configured with an application URL.
    // (The link-absent branch is covered by ResultsPage unit tests — the URL is
    // inlined at build time, so one dev server can't exercise both.)
    await expect(page.getByText(/Enrollment in Summer EBT for 2026 is now closed/i)).toBeVisible()
    await expect(page.getByTestId('apply-2027-link')).toBeVisible()
    await expect(page.getByTestId('portal-link')).toHaveCount(0)
  })

  test('mixed household: enrolled summary, not-enrolled list, and numbered next steps', async ({
    page
  }) => {
    await mockCheckResults(page, [
      makeResult({ checkId: '1', firstName: 'Jane', status: 'Match' }),
      makeResult({ checkId: '2', firstName: 'Alex', dateOfBirth: '2017-09-02', status: 'NonMatch' })
    ])

    await completeCheckFlow(page)

    await expect(page.getByTestId('not-enrolled-inline')).toBeVisible()
    await expect(page.getByTestId('next-steps')).toBeVisible()
    await expect(page.getByTestId('next-step-portal')).toBeVisible()
    await expect(page.getByTestId('next-step-apply-2027')).toBeVisible()
    await expect(page.getByText(/Enrollment in Summer EBT for 2026 is now closed/i)).toBeVisible()
  })

  test('failed check: no-information summary with closure copy', async ({ page }) => {
    await mockCheckResults(page, [makeResult({ status: 'Error' })])

    await completeCheckFlow(page)

    await expect(page.getByTestId('no-info-summary-box')).toBeVisible()
    await expect(page.getByText(/Enrollment in Summer EBT for 2026 is now closed/i)).toBeVisible()
    await expect(page.getByTestId('portal-link')).toHaveCount(0)
  })
})
