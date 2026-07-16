import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import { DC_ID_PROOF_IN_PROGRESS_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC insufficient IAL redirect (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test('redirects to id-proofing when household data returns 403 with requiredIal', async ({
    page
  }) => {
    await loginWithEmailOtpExpecting(
      page,
      DC_ID_PROOF_IN_PROGRESS_EMAIL,
      /\/login\/id-proofing\/?$/
    )

    await page.goto('/dashboard')

    await expect(page).toHaveURL(/\/login\/id-proofing\/?$/, { timeout: 15_000 })
    await expect(page.locator('#id-proofing-title')).toBeVisible()
  })
})
