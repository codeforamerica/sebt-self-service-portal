import { describe, expect, it } from 'vitest'
import { getSubmitErrorMessage } from './submitErrorCopy'

describe('getSubmitErrorMessage', () => {
  it('returns the English maintenance copy for a non-rate-limit error', () => {
    const message = getSubmitErrorMessage('maintenance', 'en')
    expect(message).toContain('system maintenance')
    expect(message).toContain('June 13')
  })

  it('returns the Spanish maintenance copy for a non-rate-limit error', () => {
    const message = getSubmitErrorMessage('maintenance', 'es')
    expect(message).toContain('mantenimiento del sistema')
    expect(message).toContain('13 de junio')
  })

  it('returns the English rate-limit copy for a rate-limit error', () => {
    expect(getSubmitErrorMessage('rateLimit', 'en')).toMatch(/too many requests/i)
  })

  it('returns the Spanish rate-limit copy for a rate-limit error', () => {
    expect(getSubmitErrorMessage('rateLimit', 'es')).toMatch(/demasiadas solicitudes/i)
  })

  it('normalizes a region-tagged Spanish locale to Spanish', () => {
    expect(getSubmitErrorMessage('maintenance', 'es-US')).toBe(getSubmitErrorMessage('maintenance', 'es'))
  })

  it('falls back to English for an unknown language', () => {
    expect(getSubmitErrorMessage('maintenance', 'fr')).toBe(getSubmitErrorMessage('maintenance', 'en'))
  })

  it('falls back to English when the language is undefined', () => {
    expect(getSubmitErrorMessage('maintenance', undefined)).toBe(getSubmitErrorMessage('maintenance', 'en'))
  })
})
