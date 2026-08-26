import { afterEach, describe, expect, it, vi } from 'vitest'

import { getFlowConfig } from './flowConfig'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getFlowConfig', () => {
  // CO confirms a collected list on /review before submitting; DC submits
  // straight from the form and never reaches /review.
  it('reports a review step for CO and none for DC', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    expect(getFlowConfig().useReviewStep).toBe(true)

    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getFlowConfig().useReviewStep).toBe(false)
  })

  // DC's backend lands unmatched input on not-enrolled rather than reporting a
  // distinct "no record found", so a separate no-results outcome cannot occur.
  it('reports a distinct no-results outcome for CO but not DC', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    expect(getFlowConfig().distinguishNoResults).toBe(true)

    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getFlowConfig().distinguishNoResults).toBe(false)
  })

  it('falls back to a configured state rather than throwing', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'zz')
    expect(typeof getFlowConfig().useReviewStep).toBe('boolean')
  })
})
