import { renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useEnrollmentSeason } from './useEnrollmentSeason'

const settled = { isPending: false, failureCount: 0 }

let mockQuery: Record<string, unknown> = { data: {}, ...settled }

vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => mockQuery
}))

const season = () => renderHook(() => useEnrollmentSeason()).result.current

describe('useEnrollmentSeason', () => {
  beforeEach(() => {
    mockQuery = { data: {}, ...settled }
  })

  it('reports a closed season when the enrollment flag is off', () => {
    mockQuery = { data: { enrollment: { enabled: false } }, ...settled }
    expect(season().season).toBe('closed')
  })

  it('reports an open season when the enrollment flag is on', () => {
    mockQuery = { data: { enrollment: { enabled: true } }, ...settled }
    expect(season().season).toBe('open')
  })

  // Fail open: the copy this drives tells families whether they can still be
  // enrolled, so a gap in the payload must not answer that question for them.
  it('reports an open season when the payload omits the enrollment field', () => {
    mockQuery = { data: { apply: { enabled: true } }, ...settled }
    expect(season().season).toBe('open')
  })

  it('reports an open season when the features fetch has not resolved', () => {
    mockQuery = { data: undefined, isPending: true, failureCount: 0 }
    expect(season().season).toBe('open')
  })

  describe('resolution', () => {
    it('is resolving while the first poll is in flight', () => {
      mockQuery = { data: undefined, isPending: true, failureCount: 0 }
      expect(season().isResolving).toBe(true)
    })

    // Otherwise a checker that cannot reach the features endpoint would hold an
    // empty page through every retry.
    it('stops resolving as soon as a poll fails', () => {
      mockQuery = { data: undefined, isPending: true, failureCount: 1 }
      expect(season().isResolving).toBe(false)
    })

    it('is resolved once the poll succeeds', () => {
      mockQuery = { data: { enrollment: { enabled: false } }, ...settled }
      expect(season().isResolving).toBe(false)
    })
  })
})
