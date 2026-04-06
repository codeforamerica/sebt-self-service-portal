import { describe, expect, it } from 'vitest'
import { formatUsPhone } from './schema'

describe('formatUsPhone', () => {
  it('formats a 10-digit string with hyphens', () => {
    expect(formatUsPhone('3035550100')).toBe('303-555-0100')
  })

  it('formats another 10-digit number', () => {
    expect(formatUsPhone('8185551234')).toBe('818-555-1234')
  })

  it('strips formatting before applying hyphens', () => {
    expect(formatUsPhone('(303) 555-0100')).toBe('303-555-0100')
    expect(formatUsPhone('303.555.0100')).toBe('303-555-0100')
    expect(formatUsPhone('303 555 0100')).toBe('303-555-0100')
  })

  it('strips leading US country code', () => {
    expect(formatUsPhone('+13035550100')).toBe('303-555-0100')
    expect(formatUsPhone('13035550100')).toBe('303-555-0100')
    expect(formatUsPhone('+1 (303) 555-0100')).toBe('303-555-0100')
  })

  it('returns the input unchanged if fewer than 10 digits', () => {
    expect(formatUsPhone('555-0100')).toBe('555-0100')
    expect(formatUsPhone('12345')).toBe('12345')
    expect(formatUsPhone('')).toBe('')
  })

  it('returns the input unchanged if more than 11 digits', () => {
    expect(formatUsPhone('123456789012')).toBe('123456789012')
  })
})
