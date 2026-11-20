import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { CHECKER_STATES } from '@/lib/checkerAssetPath'
import { getFlowConfig } from '@/lib/flowConfig'
import { CLOSED_NOT_ENROLLED_TITLE_KEY } from './closedSeasonKeys'

// A single-outcome results page prints this key as its heading. A state without
// the row would show the key name, and the component tests load one state's copy.
describe('closed-season heading copy', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  for (const state of CHECKER_STATES) {
    it(`covers every language ${state} ships, when ${state} answers one child at a time`, () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', state)
      if (getFlowConfig().resultsLayout !== 'singleOutcome') {
        return
      }

      for (const lang of ['en', 'es', 'am']) {
        const file = path.join(__dirname, '../../../../content/locales', lang, state, 'result.json')
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        if (!existsSync(file)) {
          continue
        }
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        const copy = JSON.parse(readFileSync(file, 'utf8')) as Record<string, string>

        expect(
          copy[CLOSED_NOT_ENROLLED_TITLE_KEY],
          `${lang}/${state} missing ${CLOSED_NOT_ENROLLED_TITLE_KEY}`
        ).toBeTruthy()
      }
    })
  }
})
