import { describe, expect, it } from 'vitest'
import { getRateLimitErrorMessage } from './submitErrorCopy'

describe('getRateLimitErrorMessage', () => {
  it('returns the English rate-limit copy', () => {
    expect(getRateLimitErrorMessage('en')).toMatch(/too many requests/i)
  })

  it('returns the Spanish rate-limit copy', () => {
    expect(getRateLimitErrorMessage('es')).toMatch(/demasiadas solicitudes/i)
  })

  it('normalizes a region-tagged Spanish locale to Spanish', () => {
    expect(getRateLimitErrorMessage('es-US')).toBe(getRateLimitErrorMessage('es'))
  })

  it('falls back to English for an unknown language', () => {
    expect(getRateLimitErrorMessage('fr')).toBe(getRateLimitErrorMessage('en'))
  })

  it('falls back to English when the language is undefined', () => {
    expect(getRateLimitErrorMessage(undefined)).toBe(getRateLimitErrorMessage('en'))
  })
})
