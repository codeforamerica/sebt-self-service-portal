import { describe, expect, it } from 'vitest'

import {
  ApplicationStatusSchema,
  CardStatusSchema,
  CoLoadedCohortSchema,
  formatUsPhone,
  HouseholdDataSchema,
  interpolateDate,
  IssuanceTypeSchema,
  toAnalyticsCohort
} from './schema'

/**
 * Backend enum definitions (source of truth for wire integers):
 *   CardStatus:        Requested=0, Mailed=1, Active=2, Deactivated=3
 *   ApplicationStatus: Unknown=0, Pending=1, Approved=2, Denied=3, UnderReview=4, Cancelled=5
 *   IssuanceType:      Unknown=0, SummerEbt=1, TanfEbtCard=2, SnapEbtCard=3
 *   CoLoadedCohort:    NonCoLoaded=0, CoLoadedOnly=1, MixedOrApplicantExcluded=2
 * Frontend-only: CoLoaded cohort parse value `Unknown` for absent/unrecognized wire values (not a backend enum member).
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

  it('maps empty string to Unknown (CBMS returns "" for denied/pending children)', () => {
    expect(CardStatusSchema.parse('')).toBe('Unknown')
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

describe('CoLoadedCohortSchema', () => {
  // Backend enum — stay aligned with SEBT.Portal.Core.Models.Household.CoLoadedCohort:
  //   NonCoLoaded=0, CoLoadedOnly=1, MixedOrApplicantExcluded=2
  it.each([
    [0, 'NonCoLoaded'],
    [1, 'CoLoadedOnly'],
    [2, 'MixedOrApplicantExcluded']
  ])('maps integer %i to "%s"', (input, expected) => {
    expect(CoLoadedCohortSchema.parse(input)).toBe(expected)
  })

  it('maps absent and null to Unknown', () => {
    expect(CoLoadedCohortSchema.parse(undefined)).toBe('Unknown')
    expect(CoLoadedCohortSchema.parse(null)).toBe('Unknown')
  })

  it('passes through string values unchanged', () => {
    expect(CoLoadedCohortSchema.parse('MixedOrApplicantExcluded')).toBe('MixedOrApplicantExcluded')
  })

  it('maps unrecognized integers to Unknown (distinct analytics bucket)', () => {
    expect(CoLoadedCohortSchema.parse(99)).toBe('Unknown')
  })
})

describe('toAnalyticsCohort', () => {
  it.each([
    ['NonCoLoaded', 'non_co_loaded'],
    ['CoLoadedOnly', 'co_loaded_only'],
    ['MixedOrApplicantExcluded', 'mixed_or_applicant_excluded'],
    ['Unknown', 'unknown']
  ] as const)('maps %s to standardized analytics value %s', (input, expected) => {
    expect(toAnalyticsCohort(input)).toBe(expected)
  })
})

describe('HouseholdDataSchema coLoadedCohort', () => {
  const minimalHousehold = {
    applications: [
      {
        applicationStatus: 'Approved' as const,
        children: [{ firstName: 'A', lastName: 'B' }],
        childrenOnApplication: 1
      }
    ]
  }

  it('defaults missing coLoadedCohort to Unknown after parse', () => {
    const data = HouseholdDataSchema.parse(minimalHousehold)
    expect(data.coLoadedCohort).toBe('Unknown')
  })

  it('preserves explicit NonCoLoaded', () => {
    const data = HouseholdDataSchema.parse({
      ...minimalHousehold,
      coLoadedCohort: 0
    })
    expect(data.coLoadedCohort).toBe('NonCoLoaded')
  })
})

describe('HouseholdDataSchema hashedAppId', () => {
  const baseFixture = {
    email: 'user@example.com',
    summerEbtCases: [],
    applications: [],
    coLoadedCohort: 0
  }

  it('passes a non-empty string through unchanged', () => {
    const parsed = HouseholdDataSchema.parse({ ...baseFixture, hashedAppId: 'abc123' })
    expect(parsed.hashedAppId).toBe('abc123')
  })

  it('coerces whitespace-only strings to null so analytics never sees a blank digest', () => {
    const parsed = HouseholdDataSchema.parse({ ...baseFixture, hashedAppId: '   ' })
    expect(parsed.hashedAppId).toBeNull()
  })

  it('coerces empty strings to null', () => {
    const parsed = HouseholdDataSchema.parse({ ...baseFixture, hashedAppId: '' })
    expect(parsed.hashedAppId).toBeNull()
  })

  it('passes null through as null', () => {
    const parsed = HouseholdDataSchema.parse({ ...baseFixture, hashedAppId: null })
    expect(parsed.hashedAppId).toBeNull()
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

describe('interpolateDate', () => {
  it('replaces English [MM/DD/YYYY] placeholder with formatted date', () => {
    expect(interpolateDate('Requested on [MM/DD/YYYY]', '2026-01-15T00:00:00Z', 'en')).toBe(
      'Requested on 01/15/2026'
    )
  })

  it('replaces Spanish [DD/MM/YYYY] placeholder with formatted date', () => {
    expect(interpolateDate('Solicitada el [DD/MM/YYYY]', '2026-01-15T00:00:00Z', 'es')).toBe(
      'Solicitada el 15/01/2026'
    )
  })

  it('strips English preposition + placeholder when no date', () => {
    expect(interpolateDate('Requested on [MM/DD/YYYY]', null, 'en')).toBe('Requested')
  })

  it('strips Spanish preposition + placeholder when no date', () => {
    expect(interpolateDate('Solicitada el [DD/MM/YYYY]', null, 'es')).toBe('Solicitada')
  })

  it('returns template unchanged when it has no placeholder', () => {
    expect(interpolateDate('Active', null, 'en')).toBe('Active')
    expect(interpolateDate('Active', '2026-01-15T00:00:00Z', 'en')).toBe('Active')
  })

  it('strips placeholder when there is no preceding connector word', () => {
    expect(interpolateDate('Requested [MM/DD/YYYY]', null, 'en')).toBe('Requested')
  })

  it('strips a standalone placeholder', () => {
    expect(interpolateDate('[MM/DD/YYYY]', null, 'en')).toBe('')
  })

  it('strips a placeholder at the start of a template', () => {
    expect(interpolateDate('[MM/DD/YYYY] requested', null, 'en')).toBe('requested')
  })
})
