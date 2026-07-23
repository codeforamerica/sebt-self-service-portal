import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { expect, test } from '@playwright/test'

import { isFullStackE2E, skipUnlessFullStack } from '../../fixtures/full-stack'
import { loginWithEmailOtpExpecting } from '../../fixtures/login-dc'
import {
  DC_CO_LOADED_NO_APPLICATION_EMAIL,
  DC_CO_LOADED_NO_CHILDREN_EMAIL,
  DC_MIXED_EMAIL
} from '../../fixtures/seed-users'
import { skipUnlessState } from '../../fixtures/state'

const authDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '../../.auth')
const coLoadedNoAppAuthFile = path.join(authDir, 'co-loaded-no-application.json')
const emptyStorageState = JSON.stringify({ cookies: [], origins: [] })

test.describe('DC co-loaded dashboard (full stack)', () => {
  test.beforeEach(() => {
    skipUnlessFullStack()
    skipUnlessState('dc')
  })

  test.describe('co-loaded-no-application session', () => {
    test.describe.configure({ mode: 'serial', timeout: 60_000 })

    test.beforeAll(async ({ browser }) => {
      fs.mkdirSync(authDir, { recursive: true })
      fs.writeFileSync(coLoadedNoAppAuthFile, emptyStorageState)

      if (!isFullStackE2E) {
        return
      }

      const context = await browser.newContext()
      const page = await context.newPage()
      await loginWithEmailOtpExpecting(page, DC_CO_LOADED_NO_APPLICATION_EMAIL, /\/dashboard\/?$/)
      await context.storageState({ path: coLoadedNoAppAuthFile })
      await context.close()
    })

    test.describe('with saved session', () => {
      test.use({ storageState: coLoadedNoAppAuthFile })

      test('hides Check existing applications CTA (DC-402)', async ({ page }) => {
        await page.goto('/dashboard')

        await expect(page.getByText('Check existing cards')).toBeVisible()
        await expect(page.getByText('Check existing applications')).toHaveCount(0)
      })

      test('shows SNAP/TANF children, address info, and cards/info link', async ({ page }) => {
        await page.goto('/dashboard')

        await expect(page.getByRole('heading', { name: /Maria E\. MartinezMOCK/i })).toBeVisible()
        await expect(page.getByText('Sophia', { exact: false }).first()).toBeVisible()
        await expect(page.getByText('James', { exact: false }).first()).toBeVisible()
        await expect(page.getByText('100 Co-Loaded Street')).toBeVisible()

        const addressInfoLink = page.getByRole('link', {
          name: /how we determine your mailing address/i
        })
        await expect(addressInfoLink).toBeVisible()
        await expect(addressInfoLink).toHaveAttribute('href', '/profile/address/info')

        const replacementLink = page.locator('[data-testid="accordion-content"] a', {
          hasText: 'Request a replacement card'
        })
        await expect(replacementLink.first()).toHaveAttribute('href', '/cards/info')
      })

      test('address info page renders SNAP/TANF guidance', async ({ page }) => {
        await page.goto('/profile/address/info')

        await expect(
          page.getByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
        ).toBeVisible()
        await expect(
          page.getByRole('link', { name: /learn how to get a replacement snap or tanf ebt card/i })
        ).toHaveAttribute('href', '/cards/info')
      })
    })
  })

  test('co-loaded-no-children shows empty-state applications alert', async ({ page }) => {
    test.setTimeout(60_000)
    await loginWithEmailOtpExpecting(page, DC_CO_LOADED_NO_CHILDREN_EMAIL, /\/dashboard\/?$/)

    await expect(page.getByRole('heading', { name: /Noelle C\. ChildlessMOCK/i })).toBeVisible()
    await expect(
      page.getByRole('heading', {
        name: /we are unable to find your record in the portal at this time/i
      })
    ).toBeVisible()
    await expect(page.getByText('Check existing cards')).toHaveCount(0)
    await expect(page.getByText('Check existing applications')).toHaveCount(0)
  })

  test('dc-mixed household suppresses the co-loaded child from enrolled cases', async ({
    page
  }) => {
    test.setTimeout(60_000)
    await loginWithEmailOtpExpecting(page, DC_MIXED_EMAIL, /\/dashboard\/?$/)

    await expect(page.getByRole('heading', { name: /Wei ChenMOCK/i })).toBeVisible()
    // Non-co-loaded Summer Ebt case remains as an enrolled-child accordion.
    await expect(page.getByRole('button', { name: /Aiden Chen/i })).toBeVisible()
    // Co-loaded SNAP case is filtered from SummerEbtCases (may still appear under Applications).
    await expect(page.getByRole('button', { name: /Lily Chen/i })).toHaveCount(0)
    // Remaining non-co-loaded case keeps self-service address updates available.
    await expect(
      page.getByRole('link', { name: /change my mailing address/i }).first()
    ).toBeVisible()
  })
})
