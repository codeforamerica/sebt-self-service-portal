import { describe, expect, it } from 'vitest'
import { formatUsPhone, interpolateDate } from './schema'

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

describe('interpolateDate', () => {
  it('replaces English [MM/DD/YYYY] placeholder with formatted date', () => {
    expect(interpolateDate('Requested on [MM/DD/YYYY]', '2026-01-15T00:00:00Z', 'en')).toBe(
      'Requested on 01/15/2026'
    )
  })

  it('replaces Spanish [DD/MM/YYYY] placeholder with formatted date', () => {
    expect(interpolateDate('Solicitada el [DD/MM/YYYY]', '2026-01-15T00:00:00Z', 'es')).toBe(
      'Solicitada el 15/01/2026'
    )
  })

  it('strips English preposition + placeholder when no date', () => {
    expect(interpolateDate('Requested on [MM/DD/YYYY]', null, 'en')).toBe('Requested')
  })

  it('strips Spanish preposition + placeholder when no date', () => {
    expect(interpolateDate('Solicitada el [DD/MM/YYYY]', null, 'es')).toBe('Solicitada')
  })

  it('returns template unchanged when it has no placeholder', () => {
    expect(interpolateDate('Active', null, 'en')).toBe('Active')
    expect(interpolateDate('Active', '2026-01-15T00:00:00Z', 'en')).toBe('Active')
  })

  it('strips placeholder when there is no preceding connector word', () => {
    expect(interpolateDate('Requested [MM/DD/YYYY]', null, 'en')).toBe('Requested')
  })

  it('strips a standalone placeholder', () => {
    expect(interpolateDate('[MM/DD/YYYY]', null, 'en')).toBe('')
  })

  it('strips a placeholder at the start of a template', () => {
    expect(interpolateDate('[MM/DD/YYYY] requested', null, 'en')).toBe('requested')
  })
})
