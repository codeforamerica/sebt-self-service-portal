/**
 * SubmitIdProofingRequestSchema unit tests.
 *
 * Phase 1 of DC-296 guards against malformed payloads reaching the backend
 * (and therefore Socure). These cases cover:
 *   - SSN/ITIN must be exactly 9 digits after stripping non-digits.
 *   - DOB must be a real calendar date, not in the future, not >120 years ago.
 *
 * snapAccountId, snapPersonId, and medicaidId are intentionally not shape-checked
 * here: snap ids are excluded from Socure enrichment, and medicaidId length has
 * an open product/policy question (DC CSV says "7 or 8 digits", Socure expects
 * "4 or 9").
 */
import { describe, expect, it } from 'vitest'

import { SubmitIdProofingRequestSchema } from './schema'

// Base payload with a known-good DOB. Tests override only what they exercise.
const GOOD_DOB = { month: '03', day: '10', year: '1990' }

function basePayload(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    dateOfBirth: GOOD_DOB,
    idType: 'ssn' as const,
    idValue: '999999999',
    ...overrides
  }
}

function issueOnPath(
  result: ReturnType<typeof SubmitIdProofingRequestSchema.safeParse>,
  path: string
) {
  if (result.success) return undefined
  return result.error.issues.find((i) => i.path.join('.') === path)
}

describe('SubmitIdProofingRequestSchema — SSN shape', () => {
  it('accepts a 9-digit SSN', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(basePayload({ idValue: '123456789' }))
    expect(result.success).toBe(true)
  })

  it('rejects an 8-digit SSN with an idValue error', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(basePayload({ idValue: '12345678' }))
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })

  it('rejects a 10-digit SSN with an idValue error', () => {
    // This is the exact failure mode observed in DC-296: 10 digits landed in
    // Socure's national_id and triggered blanket 400s.
    const result = SubmitIdProofingRequestSchema.safeParse(basePayload({ idValue: '1234567890' }))
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })

  it('rejects a non-numeric SSN with an idValue error', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(basePayload({ idValue: 'abcdefghi' }))
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })
})

describe('SubmitIdProofingRequestSchema — ITIN shape', () => {
  it('accepts a 9-digit ITIN', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({ idType: 'itin', idValue: '912345678' })
    )
    expect(result.success).toBe(true)
  })

  it('rejects an 8-digit ITIN with an idValue error', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({ idType: 'itin', idValue: '12345678' })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })

  it('rejects a 10-digit ITIN with an idValue error', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({ idType: 'itin', idValue: '1234567890' })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })

  it('rejects a non-numeric ITIN with an idValue error', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({ idType: 'itin', idValue: 'abcdefghi' })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'idValue')).toBeDefined()
  })
})

describe('SubmitIdProofingRequestSchema — SSN/ITIN non-digit stripping', () => {
  // The UI strips non-digits before submission, but defence-in-depth: validate
  // against the digit-only form so a dashed input "555-44-3333" parses as 9.
  it('accepts a dashed SSN whose digit count is 9', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(basePayload({ idValue: '555-44-3333' }))
    expect(result.success).toBe(true)
  })
})

describe('SubmitIdProofingRequestSchema — DOB validation', () => {
  it('rejects a DOB in the future', () => {
    const future = new Date()
    future.setFullYear(future.getFullYear() + 1)
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: {
          month: String(future.getMonth() + 1).padStart(2, '0'),
          day: String(future.getDate()).padStart(2, '0'),
          year: String(future.getFullYear())
        }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })

  it('rejects Feb 30 as an invalid calendar date', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '02', day: '30', year: '1990' }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })

  it('rejects Feb 29 in a non-leap year', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '02', day: '29', year: '2023' }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })

  it('accepts Feb 29 in a leap year', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '02', day: '29', year: '2020' }
      })
    )
    expect(result.success).toBe(true)
  })

  it('rejects a DOB more than 120 years ago', () => {
    const tooOldYear = new Date().getFullYear() - 121
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '01', day: '15', year: String(tooOldYear) }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })

  it('rejects a month outside 1-12', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '13', day: '15', year: '1990' }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })

  it('rejects a day of 0', () => {
    const result = SubmitIdProofingRequestSchema.safeParse(
      basePayload({
        idType: null,
        idValue: null,
        dateOfBirth: { month: '06', day: '00', year: '1990' }
      })
    )
    expect(result.success).toBe(false)
    expect(issueOnPath(result, 'dateOfBirth')).toBeDefined()
  })
})
