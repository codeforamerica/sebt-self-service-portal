import { describe, expect, it } from 'vitest'

import { CaseRefSchema, RequestCardReplacementSchema } from './schema'

describe('CaseRefSchema', () => {
  it('accepts a CaseRef with only summerEbtCaseId', () => {
    const result = CaseRefSchema.safeParse({ summerEbtCaseId: 'SEBT-001' })
    expect(result.success).toBe(true)
  })

  it('accepts a CaseRef with all three fields', () => {
    const result = CaseRefSchema.safeParse({
      summerEbtCaseId: 'SEBT-001',
      applicationId: 'APP-1',
      applicationStudentId: 'STU-1'
    })
    expect(result.success).toBe(true)
  })

  it('accepts null applicationId and applicationStudentId', () => {
    const result = CaseRefSchema.safeParse({
      summerEbtCaseId: 'SEBT-001',
      applicationId: null,
      applicationStudentId: null
    })
    expect(result.success).toBe(true)
  })

  it('rejects an empty summerEbtCaseId', () => {
    const result = CaseRefSchema.safeParse({ summerEbtCaseId: '' })
    expect(result.success).toBe(false)
  })

  it('rejects a missing summerEbtCaseId', () => {
    const result = CaseRefSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})

describe('RequestCardReplacementSchema', () => {
  it('accepts a request with one CaseRef', () => {
    const result = RequestCardReplacementSchema.safeParse({
      caseRefs: [{ summerEbtCaseId: 'SEBT-001' }]
    })
    expect(result.success).toBe(true)
  })

  it('accepts a request with multiple CaseRefs (mixed shape)', () => {
    const result = RequestCardReplacementSchema.safeParse({
      caseRefs: [
        { summerEbtCaseId: 'SEBT-001' },
        {
          summerEbtCaseId: 'SEBT-002',
          applicationId: 'APP-2',
          applicationStudentId: 'STU-2'
        }
      ]
    })
    expect(result.success).toBe(true)
  })

  it('rejects an empty caseRefs array', () => {
    const result = RequestCardReplacementSchema.safeParse({ caseRefs: [] })
    expect(result.success).toBe(false)
  })

  it('rejects missing caseRefs field', () => {
    const result = RequestCardReplacementSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})
