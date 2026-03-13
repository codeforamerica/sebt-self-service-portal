import { describe, expect, it } from 'vitest'
import { childSchema } from './childSchema'

describe('childSchema', () => {
  const valid = {
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12'
  }

  it('accepts valid child with required fields', () => {
    expect(childSchema.safeParse(valid).success).toBe(true)
  })

  it('accepts optional middleName', () => {
    expect(childSchema.safeParse({ ...valid, middleName: 'Marie' }).success).toBe(true)
  })

  it('rejects empty firstName', () => {
    const result = childSchema.safeParse({ ...valid, firstName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects empty lastName', () => {
    const result = childSchema.safeParse({ ...valid, lastName: '' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid DOB format', () => {
    const result = childSchema.safeParse({ ...valid, dateOfBirth: '04/12/2015' })
    expect(result.success).toBe(false)
  })

  it('rejects missing DOB', () => {
    const { dateOfBirth: _, ...noDate } = valid
    const result = childSchema.safeParse(noDate)
    expect(result.success).toBe(false)
  })
})
