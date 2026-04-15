import { describe, expect, it } from 'vitest'

import { RequestCardReplacementSchema } from './schema'

describe('RequestCardReplacementSchema', () => {
  it('accepts valid request with one case ID', () => {
    const result = RequestCardReplacementSchema.safeParse({
      caseIds: ['SEBT-001']
    })
    expect(result.success).toBe(true)
  })

  it('accepts valid request with multiple case IDs', () => {
    const result = RequestCardReplacementSchema.safeParse({
      caseIds: ['SEBT-001', 'SEBT-002', 'SEBT-003']
    })
    expect(result.success).toBe(true)
  })

  it('rejects empty case IDs array', () => {
    const result = RequestCardReplacementSchema.safeParse({
      caseIds: []
    })
    expect(result.success).toBe(false)
  })

  it('rejects missing caseIds field', () => {
    const result = RequestCardReplacementSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})
