/**
 * Every checker screen resolves its artwork through getCheckerAssetPath, which
 * interpolates the active state (checkerAssetPath.ts). If a state's manifest
 * names a file this app does not ship, that screen renders a broken image — and
 * because the failure is per-state, a CO-only test run would never see it.
 *
 * This walks every state x every slot and asserts the resolved path exists in
 * the public directory. Slots a state omits resolve to undefined and are
 * skipped: that is the documented "this state has no artwork here" case, not a
 * gap. Adding a state means dropping files into public/images/states/{state}/;
 * this test is what fails loudly when one is missed.
 */
import { existsSync } from 'node:fs'
import path from 'node:path'

import { afterEach, describe, expect, it, vi } from 'vitest'

import { CHECKER_ASSETS, CHECKER_STATES, getCheckerAssetPath } from './checkerAssetPath'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('checker asset files', () => {
  it.each(CHECKER_STATES)('ships every asset the %s manifest references', (state) => {
    vi.stubEnv('NEXT_PUBLIC_STATE', state)
    // A base path would prefix a URL segment that is not part of the on-disk
    // layout, so resolve against a bare path.
    vi.stubEnv('NEXT_PUBLIC_BASE_PATH', '')

    for (const asset of CHECKER_ASSETS) {
      const resolved = getCheckerAssetPath(asset)
      if (!resolved) {
        continue
      }

      const file = path.join(__dirname, '../../public', resolved)
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read; path is built from the static asset manifest
      expect(existsSync(file), `${resolved} (${asset}) missing from public/`).toBe(true)
    }
  })
})
