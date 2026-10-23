import { describe, expect, it } from 'vitest'

import { formatPersonName } from './names'

describe('formatPersonName', () => {
  describe('re-cases uniformly-cased names', () => {
    it.each([
      ['JOHN SMITH', 'John Smith'],
      ['john smith', 'John Smith'],
      ['DELLA', 'Della'],
      ['keigwin', 'Keigwin']
    ])('%s -> %s', (input, expected) => {
      expect(formatPersonName(input)).toBe(expected)
    })

    it('capitalizes after a hyphen', () => {
      expect(formatPersonName('MARY-JANE OLIVER')).toBe('Mary-Jane Oliver')
    })

    it.each([
      ['O’BRIEN', 'O’Brien'],
      ['o’brien', 'O’Brien'],
      ["O'BRIEN", "O'Brien"]
    ])('capitalizes after an apostrophe: %s -> %s', (input, expected) => {
      expect(formatPersonName(input)).toBe(expected)
    })

    it('capitalizes initials after a period', () => {
      expect(formatPersonName('J. R. EWING')).toBe('J. R. Ewing')
    })

    it('is Unicode-aware, not ASCII-only', () => {
      expect(formatPersonName('ÉLODIE')).toBe('Élodie')
      expect(formatPersonName('NGUYỄN')).toBe('Nguyễn')
    })

    it('capitalizes particles like any other word', () => {
      expect(formatPersonName('ANNA DE LA CRUZ')).toBe('Anna De La Cruz')
    })
  })

  describe('keeps generational suffixes uppercase', () => {
    it.each([
      ['JOHN SMITH III', 'John Smith III'],
      ['john smith ii', 'John Smith II'],
      ['MARIA GARCIA IV', 'Maria Garcia IV']
    ])('%s -> %s', (input, expected) => {
      expect(formatPersonName(input)).toBe(expected)
    })

    it('title-cases Jr and Sr rather than shouting them', () => {
      expect(formatPersonName('john smith jr')).toBe('John Smith Jr')
      expect(formatPersonName('JOHN SMITH SR')).toBe('John Smith Sr')
    })

    it('keeps a suffix uppercase when it trails a surname in the same field', () => {
      expect(formatPersonName('SMITH III')).toBe('Smith III')
    })
  })

  describe('does not mistake a name for a suffix', () => {
    it.each([
      ['VI', 'Vi'],
      ['vi', 'Vi'],
      ['IX', 'Ix'],
      ['IV', 'Iv']
    ])('title-cases a lone numeral-shaped name: %s -> %s', (input, expected) => {
      expect(formatPersonName(input)).toBe(expected)
    })

    it.each([
      ['VI DANG', 'Vi Dang'],
      ['vi nguyen', 'Vi Nguyen']
    ])('title-cases a numeral-shaped name that leads: %s -> %s', (input, expected) => {
      expect(formatPersonName(input)).toBe(expected)
    })

    it('leaves a middle initial that looks like a numeral in caps', () => {
      expect(formatPersonName('MARY V SMITH')).toBe('Mary V Smith')
    })
  })

  describe('preserves names that already carry a casing signal', () => {
    it.each([
      'MacDonald',
      'd’Alembert',
      'van der Berg',
      'McIntyre',
      'DeShawn',
      'LaToya',
      'JoAnne',
      'van Gogh',
      'Mary-Jane Oliver',
      'O’Brien',
      'John Smith',
      'MartinezMOCK',
      'DoeMOCK'
    ])('leaves %s untouched', (input) => {
      expect(formatPersonName(input)).toBe(input)
    })

    it('ignores non-letters when judging whether casing is mixed', () => {
      expect(formatPersonName("O'BRIEN-SMITH")).toBe("O'Brien-Smith")
    })
  })

  describe('leaves input it cannot improve alone', () => {
    it.each(['', ' ', '   ', 'X', 'J.'])('returns %o unchanged', (input) => {
      expect(formatPersonName(input)).toBe(input)
    })

    it('never trims or reflows whitespace', () => {
      expect(formatPersonName('  JOHN   SMITH ')).toBe('  John   Smith ')
    })

    it('tolerates a nullish value from a nullable API field', () => {
      expect(formatPersonName(undefined as unknown as string)).toBeUndefined()
      expect(formatPersonName(null as unknown as string)).toBeNull()
    })
  })
})
