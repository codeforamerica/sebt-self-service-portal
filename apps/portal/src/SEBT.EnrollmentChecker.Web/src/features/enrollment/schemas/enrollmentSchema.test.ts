import { describe, expect, it } from 'vitest'
import { enrollmentCheckResponseSchema, mapApiStatus } from './enrollmentSchema'

describe('enrollmentCheckResponseSchema', () => {
  it('parses a valid Match result', () => {
    const raw = {
      results: [{
        checkId: 'abc',
        firstName: 'Jane',
        lastName: 'Doe',
        dateOfBirth: '2015-04-12',
        status: 'Match'
      }],
      message: null
    }
    const result = enrollmentCheckResponseSchema.safeParse(raw)
    expect(result.success).toBe(true)
  })

  it('parses NonMatch and Error statuses', () => {
    const raw = {
      results: [
        { checkId: '1', firstName: 'A', lastName: 'B', dateOfBirth: '2015-01-01', status: 'NonMatch' },
        { checkId: '2', firstName: 'C', lastName: 'D', dateOfBirth: '2016-01-01', status: 'Error', statusMessage: 'Service unavailable' }
      ]
    }
    expect(enrollmentCheckResponseSchema.safeParse(raw).success).toBe(true)
  })
})

describe('mapApiStatus', () => {
  it('maps Match to enrolled', () => expect(mapApiStatus('Match')).toBe('enrolled'))
  it('maps PossibleMatch to enrolled', () => expect(mapApiStatus('PossibleMatch')).toBe('enrolled'))
  it('maps NonMatch to notEnrolled', () => expect(mapApiStatus('NonMatch')).toBe('notEnrolled'))
  it('maps Error to error', () => expect(mapApiStatus('Error')).toBe('error'))
  it('maps unknown to error', () => expect(mapApiStatus('Unknown')).toBe('error'))
})
