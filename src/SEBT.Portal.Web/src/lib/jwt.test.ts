import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  ID_PROOFING_COMPLETED_AT_CLAIM,
  ID_PROOFING_EXPIRES_AT_CLAIM,
  isIdProofingCompletionFresh,
  parseIdProofingMaxAgeYears
} from './jwt'

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

function base64UrlEncodeJson(obj: Record<string, unknown>): string {
  const json = JSON.stringify(obj)
  return btoa(json).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** Minimal unsigned JWT payload (middle segment only is read by decodeJwtPayload). */
function buildTokenWithIdProofingCompletedAt(unixSeconds: number): string {
  const payload = base64UrlEncodeJson({
    ial: '1plus',
    [ID_PROOFING_COMPLETED_AT_CLAIM]: unixSeconds
  })
  return `x.${payload}.x`
}

describe('isIdProofingCompletionFresh', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-15T12:00:00.000Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns false for null token', () => {
    expect(isIdProofingCompletionFresh(null, 5)).toBe(false)
  })

  it('returns false when claim missing', () => {
    const payload = base64UrlEncodeJson({ ial: '1plus' })
    expect(isIdProofingCompletionFresh(`x.${payload}.y`, 5)).toBe(false)
  })

  it('returns true when completion is within max age', () => {
    // ~1 year before fixed "now"
    const unix = Math.floor(new Date('2025-01-01T00:00:00.000Z').getTime() / 1000)
    const token = buildTokenWithIdProofingCompletedAt(unix)
    expect(isIdProofingCompletionFresh(token, 5)).toBe(true)
  })

  it('returns false when completion is older than max age', () => {
    const unix = Math.floor(new Date('2015-01-01T00:00:00.000Z').getTime() / 1000)
    const token = buildTokenWithIdProofingCompletedAt(unix)
    expect(isIdProofingCompletionFresh(token, 5)).toBe(false)
  })

  it('accepts claim as numeric string', () => {
    const unix = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const payload = base64UrlEncodeJson({
      ial: '1plus',
      [ID_PROOFING_COMPLETED_AT_CLAIM]: String(unix)
    })
    expect(isIdProofingCompletionFresh(`x.${payload}.x`, 5)).toBe(true)
  })

  it('returns false when id_proofing_expires_at is in the past even if completion is within max age', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expiredAt = Math.floor(new Date('2026-01-10T00:00:00.000Z').getTime() / 1000)
    const payload = base64UrlEncodeJson({
      ial: '1plus',
      [ID_PROOFING_COMPLETED_AT_CLAIM]: completed,
      [ID_PROOFING_EXPIRES_AT_CLAIM]: expiredAt
    })
    expect(isIdProofingCompletionFresh(`x.${payload}.x`, 5)).toBe(false)
  })

  it('returns true when id_proofing_expires_at is in the future and completion is within max age', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expiresAt = Math.floor(new Date('2027-01-01T00:00:00.000Z').getTime() / 1000)
    const payload = base64UrlEncodeJson({
      ial: '1plus',
      [ID_PROOFING_COMPLETED_AT_CLAIM]: completed,
      [ID_PROOFING_EXPIRES_AT_CLAIM]: expiresAt
    })
    expect(isIdProofingCompletionFresh(`x.${payload}.x`, 5)).toBe(true)
  })

  it('returns false at exact id_proofing_expires_at second (not fresh on or after expiry)', () => {
    const completed = Math.floor(new Date('2025-06-01T00:00:00.000Z').getTime() / 1000)
    const expiresAt = Math.floor(new Date('2026-01-15T12:00:00.000Z').getTime() / 1000)
    const payload = base64UrlEncodeJson({
      ial: '1plus',
      [ID_PROOFING_COMPLETED_AT_CLAIM]: completed,
      [ID_PROOFING_EXPIRES_AT_CLAIM]: expiresAt
    })
    expect(isIdProofingCompletionFresh(`x.${payload}.x`, 5)).toBe(false)
  })
})
