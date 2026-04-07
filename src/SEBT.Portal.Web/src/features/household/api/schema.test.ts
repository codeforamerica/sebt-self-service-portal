import { describe, expect, it } from 'vitest'

import {
  ApplicationStatusSchema,
  CardStatusSchema,
  formatUsPhone,
  IssuanceTypeSchema
} from './schema'

/**
 * These tests verify that frontend Zod enum schemas correctly map the integer
 * values sent by the .NET API (System.Text.Json default: enums as integers).
 *
 * Backend enum definitions (source of truth):
 *   CardStatus:        Requested=0, Mailed=1, Active=2, Deactivated=3
 *   ApplicationStatus: Unknown=0, Pending=1, Approved=2, Denied=3, UnderReview=4, Cancelled=5
 *   IssuanceType:      Unknown=0, SummerEbt=1, TanfEbtCard=2, SnapEbtCard=3
 */
describe('CardStatusSchema', () => {
  it.each([
    [0, 'Requested'],
    [1, 'Mailed'],
    [2, 'Active'],
    [3, 'Deactivated']
  ])('maps integer %i to "%s"', (input, expected) => {
    expect(CardStatusSchema.parse(input)).toBe(expected)
  })

  it('maps unrecognized integer to "Unknown"', () => {
    expect(CardStatusSchema.parse(99)).toBe('Unknown')
  })

  it('passes through string values unchanged', () => {
    expect(CardStatusSchema.parse('Active')).toBe('Active')
  })
})

describe('ApplicationStatusSchema', () => {
  it.each([
    [0, 'Unknown'],
    [1, 'Pending'],
    [2, 'Approved'],
    [3, 'Denied'],
    [4, 'UnderReview'],
    [5, 'Cancelled']
  ])('maps integer %i to "%s"', (input, expected) => {
    expect(ApplicationStatusSchema.parse(input)).toBe(expected)
  })
})

describe('IssuanceTypeSchema', () => {
  it.each([
    [0, 'Unknown'],
    [1, 'SummerEbt'],
    [2, 'TanfEbtCard'],
    [3, 'SnapEbtCard']
  ])('maps integer %i to "%s"', (input, expected) => {
    expect(IssuanceTypeSchema.parse(input)).toBe(expected)
  })
})

describe('formatUsPhone', () => {
  it('formats a 10-digit string with hyphens', () => {
    expect(formatUsPhone('3035550100')).toBe('303-555-0100')
  })

  it('formats another 10-digit number', () => {
    expect(formatUsPhone('8185551234')).toBe('818-555-1234')
  })

  it('strips formatting before applying hyphens', () => {
    expect(formatUsPhone('(303) 555-0100')).toBe('303-555-0100')
    expect(formatUsPhone('303.555.0100')).toBe('303-555-0100')
    expect(formatUsPhone('303 555 0100')).toBe('303-555-0100')
  })

  it('strips leading US country code', () => {
    expect(formatUsPhone('+13035550100')).toBe('303-555-0100')
    expect(formatUsPhone('13035550100')).toBe('303-555-0100')
    expect(formatUsPhone('+1 (303) 555-0100')).toBe('303-555-0100')
  })

  it('returns the input unchanged if fewer than 10 digits', () => {
    expect(formatUsPhone('555-0100')).toBe('555-0100')
    expect(formatUsPhone('12345')).toBe('12345')
    expect(formatUsPhone('')).toBe('')
  })

  it('returns the input unchanged if more than 11 digits', () => {
    expect(formatUsPhone('123456789012')).toBe('123456789012')
  })
})
