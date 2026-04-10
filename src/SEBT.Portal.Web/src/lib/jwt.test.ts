import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { SessionInfo } from '@/features/auth/context'

import { hasIal1Plus, isIdProofingCompletionFresh, parseIdProofingMaxAgeYears } from './jwt'

const EMPTY_SESSION: SessionInfo = {
  email: null,
  ial: null,
  idProofingStatus: null,
  idProofingCompletedAt: null,
  idProofingExpiresAt: null
}

function sessionWith(partial: Partial<SessionInfo>): SessionInfo {
  return { ...EMPTY_SESSION, ...partial }
}

describe('parseIdProofingMaxAgeYears', () => {
  it('defaults to 5 when missing or empty', () => {
    expect(parseIdProofingMaxAgeYears(undefined)).toBe(5)
    expect(parseIdProofingMaxAgeYears('')).toBe(5)
  })

  it('parses valid numbers and caps at 100', () => {
    expect(parseIdProofingMaxAgeYears('7')).toBe(7)
    expect(parseIdProofingMaxAgeYears('7.5')).toBe(7.5)
    expect(parseIdProofingMaxAgeYears('500')).toBe(100)
  })

  it('falls back to 5 for invalid', () => {
    expect(parseIdProofingMaxAgeYears('x')).toBe(5)
    expect(parseIdProofingMaxAgeYears('-1')).toBe(5)
  })
})

describe('hasIal1Plus', () => {
  it('returns false for null session', () => {
    expect(hasIal1Plus(null)).toBe(false)
  })

  it('returns true for 1plus and 2', () => {
    expect(hasIal1Plus(sessionWith({ ial: '1plus' }))).toBe(true)
    expect(hasIal1Plus(sessionWith({ ial: '2' }))).toBe(true)
  })

  it('returns false for 0, 1, and missing', () => {
    expect(hasIal1Plus(sessionWith({ ial: '0' }))).toBe(false)
    expect(hasIal1Plus(sessionWith({ ial: '1' }))).toBe(false)
    expect(hasIal1Plus(EMPTY_SESSION)).toBe(false)
  })
})

describe('isIdProofingCompletionFresh', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-15T12:00:00.000Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns false for null session', () => {
    expect(isIdProofingCompletionFresh(null, 5)).toBe(false)
  })

  it('returns false when completion claim is absent', () => {
    expect(isIdProofingCompletionFresh(sessionWith({ ial: '1plus' }), 5)).toBe(false)
  })

  it('returns true when completion is within max age', () => {
    const unix = Math.floor(new Date('2025-01-01T00:00:00.000Z').getTime() / 1000)
    expect(isIdProofingCompletionFresh(sessionWith({ idProofingCompletedAt: unix }), 5)).toBe(true)
  })

  it('returns false when completion is older than max age', () => {
    const unix = Math.floor(new Date('2015-01-01T00:00:00.000Z').getTime() / 1000)
    expect(isIdProofingCompletionFresh(sessionWith({ idProofingCompletedAt: unix }), 5)).toBe(false)
  })

  it('returns false when idProofingExpiresAt is in the past even if completion is fresh', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expired = Math.floor(new Date('2026-01-10T00:00:00.000Z').getTime() / 1000)
    expect(
      isIdProofingCompletionFresh(
        sessionWith({ idProofingCompletedAt: completed, idProofingExpiresAt: expired }),
        5
      )
    ).toBe(false)
  })

  it('returns true when idProofingExpiresAt is in the future and completion is fresh', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expiresAt = Math.floor(new Date('2027-01-01T00:00:00.000Z').getTime() / 1000)
    expect(
      isIdProofingCompletionFresh(
        sessionWith({ idProofingCompletedAt: completed, idProofingExpiresAt: expiresAt }),
        5
      )
    ).toBe(true)
  })

  it('returns false at exact idProofingExpiresAt (not fresh on or after expiry)', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expiresAt = Math.floor(new Date('2026-01-15T12:00:00.000Z').getTime() / 1000)
    expect(
      isIdProofingCompletionFresh(
        sessionWith({ idProofingCompletedAt: completed, idProofingExpiresAt: expiresAt }),
        5
      )
    ).toBe(false)
  })
})
