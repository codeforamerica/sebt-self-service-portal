/**
 * A manifest entry naming a file the app doesn't ship renders a broken image,
 * and because the failure is per-state a single-state test run never sees it.
 *
 * Walks every state x slot and asserts the resolved path exists on disk. Omitted
 * slots resolve to undefined and are skipped — that's "no artwork here", not a
 * gap.
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
    // A base path is a URL segment, not part of the on-disk layout.
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
