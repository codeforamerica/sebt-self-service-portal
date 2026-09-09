import { renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from './schema'
import { useRequiredHouseholdData } from './useRequiredHouseholdData'

const mockHouseholdData: HouseholdData = {
  email: 'test@example.com',
  phone: '(202) 555-0100',
  summerEbtCases: [],
  applications: [],
  addressOnFile: null,
  coLoadedCohort: 'NonCoLoaded'
}

let mockData: HouseholdData | undefined

vi.mock('./useHouseholdData', () => ({
  useHouseholdData: () => ({ data: mockData })
}))

describe('useRequiredHouseholdData', () => {
  it('returns data when useHouseholdData() has data in cache', () => {
    mockData = mockHouseholdData

    const { result } = renderHook(() => useRequiredHouseholdData())

    expect(result.current).toBe(mockHouseholdData)
    expect(result.current.email).toBe('test@example.com')
  })

  it('throws Error when useHouseholdData() returns undefined', () => {
    mockData = undefined

    expect(() => renderHook(() => useRequiredHouseholdData())).toThrow(
      'useRequiredHouseholdData must only be used in components rendered after data is loaded'
    )
  })
})
