import { describe, expect, it } from 'vitest'
import { formatUsPhone } from './schema'

describe('formatUsPhone', () => {
  it('formats a 10-digit string with hyphens', () => {
    expect(formatUsPhone('3035550100')).toBe('303-555-0100')
  })

  it('formats another 10-digit number', () => {
    expect(formatUsPhone('8185551234')).toBe('818-555-1234')
  })

  it('returns the input unchanged if not exactly 10 digits', () => {
    expect(formatUsPhone('555-0100')).toBe('555-0100')
    expect(formatUsPhone('12345678901')).toBe('12345678901')
    expect(formatUsPhone('')).toBe('')
  })

  it('returns already-formatted numbers unchanged', () => {
    expect(formatUsPhone('(303) 555-0100')).toBe('(303) 555-0100')
  })
})
