import { describe, expect, it } from 'vitest'

import { RequestCardReplacementSchema } from './schema'

describe('RequestCardReplacementSchema', () => {
  it('accepts valid request with one application number', () => {
    const result = RequestCardReplacementSchema.safeParse({
      applicationNumbers: ['APP-001']
    })
    expect(result.success).toBe(true)
  })

  it('accepts valid request with multiple application numbers', () => {
    const result = RequestCardReplacementSchema.safeParse({
      applicationNumbers: ['APP-001', 'APP-002', 'APP-003']
    })
    expect(result.success).toBe(true)
  })

  it('rejects empty application numbers array', () => {
    const result = RequestCardReplacementSchema.safeParse({
      applicationNumbers: []
    })
    expect(result.success).toBe(false)
  })

  it('rejects missing applicationNumbers field', () => {
    const result = RequestCardReplacementSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})
