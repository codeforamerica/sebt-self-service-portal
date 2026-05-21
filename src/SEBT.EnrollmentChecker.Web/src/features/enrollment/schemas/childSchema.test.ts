import { describe, expect, it } from 'vitest'
import { childFormSchema } from './childSchema'

describe('childFormSchema', () => {
  const valid = {
    firstName: 'Jane',
    lastName: 'Doe',
    month: '4',
    day: '12',
    year: '2015'
  }

  it('accepts valid child with required fields', () => {
    expect(childFormSchema.safeParse(valid).success).toBe(true)
  })

  it('accepts optional middleName', () => {
    expect(childFormSchema.safeParse({ ...valid, middleName: 'Marie' }).success).toBe(true)
  })

  it('accepts empty middleName', () => {
    const result = childFormSchema.safeParse({ ...valid, middleName: '' })
    expect(result.success).toBe(true)
  })

  describe('name validation', () => {
    it('rejects empty firstName', () => {
      expect(childFormSchema.safeParse({ ...valid, firstName: '' }).success).toBe(false)
    })

    it('rejects empty lastName', () => {
      expect(childFormSchema.safeParse({ ...valid, lastName: '' }).success).toBe(false)
    })

    it('rejects names containing digits', () => {
      const result = childFormSchema.safeParse({ ...valid, firstName: 'user1' })
      expect(result.success).toBe(false)
    })

    it('rejects names containing symbols', () => {
      const result = childFormSchema.safeParse({ ...valid, firstName: 'user %*(' })
      expect(result.success).toBe(false)
    })

    it('accepts names with Latin diacritics so transform can normalize them before send', () => {
      expect(childFormSchema.safeParse({ ...valid, firstName: 'Élian' }).success).toBe(true)
      expect(childFormSchema.safeParse({ ...valid, lastName: 'Svön' }).success).toBe(true)
    })

    it('accepts names with hyphens and apostrophes (straight and curly)', () => {
      expect(childFormSchema.safeParse({ ...valid, lastName: "O'Connor" }).success).toBe(true)
      expect(childFormSchema.safeParse({ ...valid, lastName: 'O’Connor' }).success).toBe(true)
      expect(childFormSchema.safeParse({ ...valid, lastName: 'Smith-Jones' }).success).toBe(true)
    })

    it('accepts compound first names with spaces', () => {
      expect(childFormSchema.safeParse({ ...valid, firstName: 'Mary Anne' }).success).toBe(true)
    })
  })

  describe('birth date validation', () => {
    it('rejects empty day', () => {
      expect(childFormSchema.safeParse({ ...valid, day: '' }).success).toBe(false)
    })

    it('rejects non-numeric day', () => {
      expect(childFormSchema.safeParse({ ...valid, day: 'abc' }).success).toBe(false)
    })

    it('rejects day out of range (45)', () => {
      expect(childFormSchema.safeParse({ ...valid, day: '45' }).success).toBe(false)
    })

    it('rejects day out of range (0)', () => {
      expect(childFormSchema.safeParse({ ...valid, day: '0' }).success).toBe(false)
    })

    it('rejects day 42 with valid month and year (Mar 42, 2011)', () => {
      expect(
        childFormSchema.safeParse({ ...valid, month: '3', day: '42', year: '2011' }).success
      ).toBe(false)
    })

    it('rejects empty month', () => {
      expect(childFormSchema.safeParse({ ...valid, month: '' }).success).toBe(false)
    })

    it('rejects empty year', () => {
      expect(childFormSchema.safeParse({ ...valid, year: '' }).success).toBe(false)
    })

    it('rejects malformed year (15)', () => {
      expect(childFormSchema.safeParse({ ...valid, year: '15' }).success).toBe(false)
    })

    it('rejects pre-1900 year (1801)', () => {
      expect(childFormSchema.safeParse({ ...valid, year: '1801' }).success).toBe(false)
    })

    it('rejects post-21st-century year (3000)', () => {
      expect(childFormSchema.safeParse({ ...valid, year: '3000' }).success).toBe(false)
    })

    it('rejects impossible calendar dates (Feb 31, 2020)', () => {
      const result = childFormSchema.safeParse({
        ...valid,
        month: '2',
        day: '31',
        year: '2020'
      })
      expect(result.success).toBe(false)
    })

    it('rejects dates more than 100 years ago (March 31, 1900)', () => {
      const result = childFormSchema.safeParse({
        ...valid,
        month: '3',
        day: '31',
        year: '1900'
      })
      expect(result.success).toBe(false)
    })

    it('rejects future birth dates', () => {
      const nextYear = new Date().getFullYear() + 1
      const result = childFormSchema.safeParse({
        ...valid,
        month: '6',
        day: '15',
        year: String(nextYear)
      })
      expect(result.success).toBe(false)
    })

    it('accepts a date exactly 50 years ago', () => {
      const fiftyYearsAgo = new Date().getFullYear() - 50
      const result = childFormSchema.safeParse({
        ...valid,
        month: '6',
        day: '15',
        year: String(fiftyYearsAgo)
      })
      expect(result.success).toBe(true)
    })
  })
})
