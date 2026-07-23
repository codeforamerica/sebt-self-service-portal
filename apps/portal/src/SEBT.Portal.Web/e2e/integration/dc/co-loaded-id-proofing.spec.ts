import { expect, test } from '@playwright/test'

import { isFullStackE2E, skipUnlessFullStack } from '../../fixtures/full-stack'
import { submitIdProofingForm } from '../../fixtures/id-proofing-dc'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import { reseedUserScenario } from '../../fixtures/reseed'
import {
  DC_CO_LOADED_PENDING_ID_PROOFING_EMAIL,
  DC_CO_LOADED_PENDING_ID_PROOFING_SCENARIO,
  DC_CO_LOADED_PENDING_SNAP_ACCOUNT_ID
} from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC co-loaded id proofing (full stack)', () => {
  test.describe.configure({ mode: 'serial', timeout: 60_000 })

  test.beforeAll(async () => {
    if (!isFullStackE2E) {
      return
    }

    // restore before the suite so a prior interrupted run does not leave the user stuck out of pending.
    await reseedUserScenario(DC_CO_LOADED_PENDING_ID_PROOFING_SCENARIO)
  })

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

  test('wrong co-loaded benefit ID routes to co-loaded off-boarding copy', async ({ page }) => {
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
    await expect(page).toHaveURL(/reason=coLoadedOnly/)
    await expect(page.locator('#off-boarding-title')).toBeVisible()
    await expect(
      page.getByRole('heading', {
        name: /we're sorry, we aren't able to show your dc sun bucks information/i
      })
    ).toBeVisible()
    await expect(page.getByRole('link', { name: /apply now/i })).toBeVisible()
  })

  // Mutates the seeded pending user to Completed + IsCoLoaded — keep last in this serial suite.
  test('matching SNAP account ID completes co-loaded proofing and lands on dashboard', async ({
    page
  }) => {
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
      idValue: DC_CO_LOADED_PENDING_SNAP_ACCOUNT_ID
    })

    await expect(page).toHaveURL(/\/dashboard\/?$/, { timeout: 30_000 })
    await expect(page.getByRole('heading', { name: /Maria E\. MartinezMOCK/i })).toBeVisible()
    await expect(page.getByText('Sophia', { exact: false }).first()).toBeVisible()
    await expect(page.getByText('James', { exact: false }).first()).toBeVisible()
    // Pending household is a copy of the co-loaded seed (includes applications).
    await expect(page.getByText('Check existing applications')).toBeVisible()
  })
})
