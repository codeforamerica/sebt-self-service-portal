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

  it('rejects empty firstName', () => {
    const result = childFormSchema.safeParse({ ...valid, firstName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty lastName', () => {
    const result = childFormSchema.safeParse({ ...valid, lastName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid day format', () => {
    const result = childFormSchema.safeParse({ ...valid, day: 'abc' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid year format', () => {
    const result = childFormSchema.safeParse({ ...valid, year: '15' })
    expect(result.success).toBe(false)
  })
})
