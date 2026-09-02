import type { Page } from '@playwright/test'

/** Shape of one child result returned by POST /api/enrollment/check. */
export interface MockCheckResult {
  checkId: string
  firstName: string
  lastName: string
  dateOfBirth: string
  /** API statuses: Match | NonMatch | PossibleMatch | Error */
  status: string
}

export function makeResult(overrides: Partial<MockCheckResult> = {}): MockCheckResult {
  return {
    checkId: '1',
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12',
    status: 'Match',
    ...overrides
  }
}

/** Intercepts the enrollment check API with a controlled set of child results. */
export async function mockCheckResults(page: Page, results: MockCheckResult[]): Promise<void> {
  await page.route('**/api/enrollment/check', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ results })
    })
  )
}

/**
 * Intercepts the checker features poll that drives the maintenance banner and
 * the outage guard. The outage page replaces every checker route while enabled.
 */
export async function mockFeatures(page: Page, { outage = false }: { outage?: boolean } = {}): Promise<void> {
  await page.route('**/api/enrollment/features*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        maintenanceBanner: { enabled: false, message: {} },
        outagePage: { enabled: outage }
      })
    })
  )
}

export interface ChildFormEntry {
  firstName: string
  lastName: string
  month: string
  day: string
  year: string
}

/** Fills the child form's required fields. Assumes the page is on /check. */
export async function fillChildForm(page: Page, child: ChildFormEntry): Promise<void> {
  await page.getByRole('textbox', { name: /first name/i }).fill(child.firstName)
  await page.getByRole('textbox', { name: /last name/i }).fill(child.lastName)
  await page.getByLabel(/month/i).selectOption(child.month)
  await page.getByRole('textbox', { name: /day/i }).fill(child.day)
  await page.getByRole('textbox', { name: /year/i }).fill(child.year)
}

/** Walks landing → disclaimer → child form for the first child of a session. */
export async function startCheckFlow(page: Page): Promise<void> {
  await page.goto('/')
  await page.getByRole('button', { name: /check enrollment/i }).click()
  await page.waitForURL('**/disclaimer')
  await page.getByRole('button', { name: /continue/i }).click()
  await page.waitForURL('**/check')
}

/**
 * Completes the whole flow for a single child and lands on /results.
 * Callers mock the check API (mockCheckResults) before invoking this.
 */
export async function completeCheckFlow(
  page: Page,
  child: ChildFormEntry = { firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' }
): Promise<void> {
  await startCheckFlow(page)
  await fillChildForm(page, child)
  await page.getByRole('button', { name: /continue/i }).click()
  await page.waitForURL('**/review')
  await page.getByRole('button', { name: /submit/i }).click()
  await page.waitForURL('**/results')
}
