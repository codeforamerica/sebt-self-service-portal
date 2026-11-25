import { STATE_CODES } from '@sebt/design-system/src/lib/state'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { CHECKER_STATES, getCheckerStateConfig } from './checkerStateConfig'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('CHECKER_STATES', () => {
  it('covers every state the design system knows', () => {
    expect([...CHECKER_STATES].sort()).toEqual([...STATE_CODES].sort())
  })
})

describe('getCheckerStateConfig', () => {
  it.each(STATE_CODES)('returns flow, landing and assets for %s', (state) => {
    vi.stubEnv('NEXT_PUBLIC_STATE', state)
    const config = getCheckerStateConfig()
    expect(typeof config.flow.useReviewStep).toBe('boolean')
    expect(typeof config.landing.useAccordion).toBe('boolean')
    expect(config.assets.errorCard).toBeTruthy()
  })

  it('selects the entry for the active state', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    expect(getCheckerStateConfig().flow.resultsLayout).toBe('household')

    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getCheckerStateConfig().flow.resultsLayout).toBe('singleOutcome')
  })
})
