import { describe, expect, it } from 'vitest'

import { isWithinCooldownPeriod } from './cooldown'

describe('isWithinCooldownPeriod', () => {
  it('returns true when cardRequestedAt is within 14 days', () => {
    const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString()
    expect(isWithinCooldownPeriod(threeDaysAgo)).toBe(true)
  })

  it('returns true when cardRequestedAt is exactly 13 days ago', () => {
    const thirteenDaysAgo = new Date(Date.now() - 13 * 24 * 60 * 60 * 1000).toISOString()
    expect(isWithinCooldownPeriod(thirteenDaysAgo)).toBe(true)
  })

  it('returns false when cardRequestedAt is exactly 14 days ago', () => {
    const fourteenDaysAgo = new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString()
    expect(isWithinCooldownPeriod(fourteenDaysAgo)).toBe(false)
  })

  it('returns false when cardRequestedAt is more than 14 days ago', () => {
    const thirtyDaysAgo = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString()
    expect(isWithinCooldownPeriod(thirtyDaysAgo)).toBe(false)
  })

  it('returns false when cardRequestedAt is null', () => {
    expect(isWithinCooldownPeriod(null)).toBe(false)
  })

  it('returns false when cardRequestedAt is undefined', () => {
    expect(isWithinCooldownPeriod(undefined)).toBe(false)
  })

  it('returns false for invalid date string', () => {
    expect(isWithinCooldownPeriod('not-a-date')).toBe(false)
  })
})
