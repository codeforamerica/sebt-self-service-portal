import { describe, expect, it } from 'vitest'
import {
  CBMS_FIRST_NAME_MAX,
  CBMS_LAST_NAME_MAX,
  sanitizeNameForCbms
} from './sanitizeForCbms'

describe('sanitizeNameForCbms', () => {
  it('strips Latin diacritics to plain ASCII letters', () => {
    expect(sanitizeNameForCbms('Élian', CBMS_FIRST_NAME_MAX)).toBe('Elian')
    expect(sanitizeNameForCbms('Ália', CBMS_FIRST_NAME_MAX)).toBe('Alia')
    expect(sanitizeNameForCbms('Svön', CBMS_LAST_NAME_MAX)).toBe('Svon')
  })

  it('converts curly apostrophes to straight apostrophes', () => {
    expect(sanitizeNameForCbms('O’Connel', CBMS_LAST_NAME_MAX)).toBe("O'Connel")
    expect(sanitizeNameForCbms('O‘Connel', CBMS_LAST_NAME_MAX)).toBe("O'Connel")
  })

  it('truncates first names longer than 35 characters', () => {
    const longName = 'A'.repeat(50)
    expect(sanitizeNameForCbms(longName, CBMS_FIRST_NAME_MAX)).toHaveLength(35)
  })

  it('truncates last names longer than 40 characters', () => {
    const longName = 'B'.repeat(50)
    expect(sanitizeNameForCbms(longName, CBMS_LAST_NAME_MAX)).toHaveLength(40)
  })

  it('preserves names already in CBMS-friendly form', () => {
    expect(sanitizeNameForCbms('Jane', CBMS_FIRST_NAME_MAX)).toBe('Jane')
    expect(sanitizeNameForCbms("O'Connor", CBMS_LAST_NAME_MAX)).toBe("O'Connor")
    expect(sanitizeNameForCbms('Smith-Jones', CBMS_LAST_NAME_MAX)).toBe('Smith-Jones')
    expect(sanitizeNameForCbms('Mary Anne', CBMS_FIRST_NAME_MAX)).toBe('Mary Anne')
  })

  it('handles diacritics and curly quotes combined and respects truncation', () => {
    const input = 'Élianor’s' + 'x'.repeat(40)
    const out = sanitizeNameForCbms(input, CBMS_LAST_NAME_MAX)
    expect(out.startsWith("Elianor's")).toBe(true)
    expect(out).toHaveLength(40)
  })

  it('produces output that matches the CBMS API pattern for valid Latin input', () => {
    const cbmsPattern = /^[A-Za-z\-'\s]{1,40}$/
    expect(cbmsPattern.test(sanitizeNameForCbms('Élian', CBMS_FIRST_NAME_MAX))).toBe(true)
    expect(cbmsPattern.test(sanitizeNameForCbms('O’Brien', CBMS_LAST_NAME_MAX))).toBe(true)
    expect(cbmsPattern.test(sanitizeNameForCbms('María José', CBMS_FIRST_NAME_MAX))).toBe(true)
  })
})
