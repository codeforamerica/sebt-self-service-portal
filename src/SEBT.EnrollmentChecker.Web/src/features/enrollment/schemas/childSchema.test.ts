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

  it('rejects empty firstName', () => {
    const result = childFormSchema.safeParse({ ...valid, firstName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty lastName', () => {
    const result = childFormSchema.safeParse({ ...valid, lastName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty day', () => {
    const result = childFormSchema.safeParse({ ...valid, day: '' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid day format', () => {
    const result = childFormSchema.safeParse({ ...valid, day: 'abc' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid day', () => {
    const result = childFormSchema.safeParse({ ...valid, day: '45' })
    expect(result.success).toBe(false)
  })

    it('rejects invalid day', () => {
    const result = childFormSchema.safeParse({ ...valid, day: '0' })
    expect(result.success).toBe(false)
  })

  it('rejects empty month', () => {
    const result = childFormSchema.safeParse({ ...valid, month: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty year', () => {
    const result = childFormSchema.safeParse({ ...valid, year: '' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid year format', () => {
    const result = childFormSchema.safeParse({ ...valid, year: '15' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid year', () => {
    const result = childFormSchema.safeParse({ ...valid, year: '1801' })
    expect(result.success).toBe(false)
  })

    it('rejects invalid year', () => {
    const result = childFormSchema.safeParse({ ...valid, year: '3000' })
    expect(result.success).toBe(false)
  })
})
