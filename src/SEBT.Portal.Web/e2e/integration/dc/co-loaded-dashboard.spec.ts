import { expect, test } from '@playwright/test'

import { skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import { DC_CO_LOADED_NO_APPLICATION_EMAIL } from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

test.describe('DC co-loaded dashboard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test('co-loaded-no-application hides Check existing applications CTA (DC-402)', async ({
    page
  }) => {
    await loginWithEmailOtpExpecting(page, DC_CO_LOADED_NO_APPLICATION_EMAIL, /\/dashboard\/?$/)

    await expect(page.getByText('Check existing cards')).toBeVisible()
    await expect(page.getByText('Check existing applications')).toHaveCount(0)
  })
})
