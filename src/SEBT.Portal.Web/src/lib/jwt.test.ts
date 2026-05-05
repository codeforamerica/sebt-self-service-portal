import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { SessionInfo } from '@/features/auth/context'

import { hasIal1Plus, isIdProofingCompletionFresh } from './jwt'

const EMPTY_SESSION: SessionInfo = {
  email: null,
  ial: null,
  idProofingStatus: null,
  idProofingCompletedAt: null,
  idProofingExpiresAt: null,
  isCoLoaded: null,
  expiresAt: null,
  absoluteExpiresAt: null
}

function sessionWith(partial: Partial<SessionInfo>): SessionInfo {
  return { ...EMPTY_SESSION, ...partial }
}

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
    expect(isIdProofingCompletionFresh(null)).toBe(false)
  })

  it('returns false when idProofingExpiresAt is absent', () => {
    expect(isIdProofingCompletionFresh(sessionWith({ ial: '1plus' }))).toBe(false)
  })

  it('returns true when idProofingExpiresAt is in the future', () => {
    const expiresAt = Math.floor(new Date('2027-01-01T00:00:00.000Z').getTime() / 1000)
    expect(isIdProofingCompletionFresh(sessionWith({ idProofingExpiresAt: expiresAt }))).toBe(true)
  })

  it('returns false when idProofingExpiresAt is in the past', () => {
    const expired = Math.floor(new Date('2026-01-10T00:00:00.000Z').getTime() / 1000)
    expect(isIdProofingCompletionFresh(sessionWith({ idProofingExpiresAt: expired }))).toBe(false)
  })

  it('returns false at exact idProofingExpiresAt (not fresh on or after expiry)', () => {
    const expiresAt = Math.floor(new Date('2026-01-15T12:00:00.000Z').getTime() / 1000)
    expect(isIdProofingCompletionFresh(sessionWith({ idProofingExpiresAt: expiresAt }))).toBe(false)
  })
})
