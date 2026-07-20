import { describe, expect, it } from 'vitest'

import { createMockSummerEbtCase } from '../testing'
import { mergeHouseholdCardDetails } from './mergeHouseholdCardDetails'
import type { HouseholdData } from './schema'

const shellCase = createMockSummerEbtCase({
  summerEBTCaseID: 'CASE-1',
  childFirstName: 'A',
  childLastName: 'B',
  ebtCardStatus: 'Unknown',
  ebtCardLastFour: undefined,
  cardRequestedAt: '2026-01-01T00:00:00Z'
})

const shell: HouseholdData = {
  email: 'test@example.com',
  summerEbtCases: [shellCase],
  applications: [],
  coLoadedCohort: 'NonCoLoaded'
}

const full: HouseholdData = {
  ...shell,
  summerEbtCases: [
    createMockSummerEbtCase({
      summerEBTCaseID: 'CASE-1',
      childFirstName: 'A',
      childLastName: 'B',
      ebtCardLastFour: '4321',
      ebtCardStatus: 'Active',
      ebtCardIssueDate: '2026-06-01T00:00:00Z',
      ebtCardBalance: 120,
      cardRequestedAt: '2026-01-01T00:00:00Z'
    })
  ]
}

describe('mergeHouseholdCardDetails', () => {
  it('merges card fields from full response by case id', () => {
    const merged = mergeHouseholdCardDetails(shell, full)
    const mergedCase = merged.summerEbtCases[0]

    expect(mergedCase).toBeDefined()
    expect(mergedCase!.ebtCardLastFour).toBe('4321')
    expect(mergedCase!.ebtCardStatus).toBe('Active')
    expect(mergedCase!.cardRequestedAt).toBe('2026-01-01T00:00:00Z')
  })
})
