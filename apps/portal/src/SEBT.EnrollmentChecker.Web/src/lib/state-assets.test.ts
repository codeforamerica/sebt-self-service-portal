/**
 * Every image the shared chrome renders for a state (header logo, footer seal,
 * icons) must exist in this app's public folder. The merged config mixes shared
 * and per-state paths, so a single-state run would miss a gap in the other.
 */
import { existsSync } from 'node:fs'
import path from 'node:path'

import { describe, expect, it } from 'vitest'

import { getStateConfig, STATE_CODES } from '@sebt/design-system/src/lib/state'

describe('state asset files', () => {
  it.each(STATE_CODES)('ships every image the %s config references', (state) => {
    const { logo, seal, icons } = getStateConfig(state).assets

    for (const src of [logo.src, seal.src, ...Object.values(icons)]) {
      const file = path.join(__dirname, '../../public', src)
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read; path comes from the static state config
      expect(existsSync(file), `${src} missing from public/`).toBe(true)
    }
  })
})
