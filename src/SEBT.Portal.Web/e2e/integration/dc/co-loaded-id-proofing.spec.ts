import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { submitIdProofingForm } from '../../fixtures/id-proofing-dc'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import { DC_CO_LOADED_PENDING_ID_PROOFING_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC co-loaded id proofing (full stack)', () => {
  test.describe.configure({ mode: 'serial', timeout: 60_000 })

  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test('co-loaded-pending user lands on id-proofing after OTP', async ({ page }) => {
    await loginWithEmailOtpExpecting(
      page,
      DC_CO_LOADED_PENDING_ID_PROOFING_EMAIL,
      /\/login\/id-proofing\/?$/
    )

    await expect(page.locator('#id-proofing-title')).toBeVisible()
    await expect(page.getByRole('radio', { name: /account id/i })).toBeVisible()
  })

  test('wrong co-loaded benefit ID routes to off-boarding', async ({ page }) => {
    await loginWithEmailOtpExpecting(
      page,
      DC_CO_LOADED_PENDING_ID_PROOFING_EMAIL,
      /\/login\/id-proofing\/?$/
    )

    await submitIdProofingForm(page, {
      month: '06',
      day: '15',
      year: '1985',
      idType: 'snapAccountId',
      idValue: '99999999'
    })

    await expect(page).toHaveURL(/\/login\/id-proofing\/off-boarding\/?/, { timeout: 30_000 })
    await expect(page.locator('#off-boarding-title')).toBeVisible()
  })
})
