import { expect, test } from '@playwright/test'

// AuthGuard sends visitors without a session to /login. No injectAuth here —
// the only mock is the status probe answering "not signed in" (the 401 form
// stays understood by the client for rollout compatibility).
test.describe('Unauthenticated visits redirect to sign in', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/status*', (route) =>
      route.fulfill({ status: 401, contentType: 'application/json', body: '{}' })
    )
  })

  for (const path of ['/dashboard', '/cards/request', '/profile/address']) {
    test(`visiting ${path} while signed out lands on the login page`, async ({ page }) => {
      await page.goto(path)

      await page.waitForURL('**/login')
      await expect(page).toHaveURL(/\/login/)
    })
  }
})
