import { describe, expect, it } from 'vitest'

import type { IssuanceType } from '@/features/household/api'
import { createMockApplication, createMockSummerEbtCase } from '@/features/household/testing'

import { getColoadingStatus } from './coloadingStatus'

function household({ cases = [] as IssuanceType[], applications = 0 } = {}) {
  return {
    summerEbtCases: cases.map((issuanceType) => createMockSummerEbtCase({ issuanceType })),
    applications: Array.from({ length: applications }, () => createMockApplication())
  }
}

describe('getColoadingStatus', () => {
  it('returns non_co_loaded when session.isCoLoaded is false', () => {
    expect(getColoadingStatus(false, household({ cases: ['SummerEbt'] }))).toBe('non_co_loaded')
  })

  it('returns unknown when session.isCoLoaded is null (auth not resolved)', () => {
    expect(getColoadingStatus(null, household())).toBe('unknown')
  })

  it('returns unknown when session.isCoLoaded is undefined (no session yet)', () => {
    expect(getColoadingStatus(undefined, household())).toBe('unknown')
  })

  it('returns co_loaded_only when co-loaded with only SnapEbtCard/TanfEbtCard cases and no applications', () => {
    const data = household({ cases: ['SnapEbtCard', 'TanfEbtCard'], applications: 0 })
    expect(getColoadingStatus(true, data)).toBe('co_loaded_only')
  })

  it('returns co_loaded_only when co-loaded with no cases and no applications', () => {
    expect(getColoadingStatus(true, household())).toBe('co_loaded_only')
  })

  it('returns mixed_eligibility when co-loaded but at least one case is SummerEbt', () => {
    const data = household({ cases: ['SnapEbtCard', 'SummerEbt'] })
    expect(getColoadingStatus(true, data)).toBe('mixed_eligibility')
  })

  it('returns mixed_eligibility when co-loaded with any submitted application', () => {
    const data = household({ cases: ['TanfEbtCard'], applications: 1 })
    expect(getColoadingStatus(true, data)).toBe('mixed_eligibility')
  })
})
