import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'

import type { HouseholdData } from '@/features/household/api'

import { patchHouseholdAddressOnFileCache } from './client'

const BASE_HOUSEHOLD: HouseholdData = {
  email: 'test@example.com',
  summerEbtCases: [],
  applications: [],
  addressOnFile: {
    streetAddress1: '1350 Pennsylvania Ave NW',
    streetAddress2: null,
    city: 'Washington',
    state: 'DC',
    postalCode: '20004'
  },
  coLoadedCohort: 'NonCoLoaded'
}

describe('patchHouseholdAddressOnFileCache', () => {
  it('updates addressOnFile in householdData cache when status is valid', () => {
    const queryClient = new QueryClient()
    queryClient.setQueryData(['householdData'], BASE_HOUSEHOLD)

    patchHouseholdAddressOnFileCache(
      queryClient,
      {
        status: 'valid',
        normalizedAddress: {
          streetAddress1: '456 Oak Avenue NW',
          streetAddress2: null,
          city: 'Washington',
          state: 'DC',
          postalCode: '20002'
        }
      },
      {
        streetAddress1: '456 Oak Avenue NW',
        city: 'Washington',
        state: 'DC',
        postalCode: '20002'
      }
    )

    const updated = queryClient.getQueryData<HouseholdData>(['householdData'])
    expect(updated?.addressOnFile?.streetAddress1).toBe('456 Oak Avenue NW')
  })

  it('falls back to submitted address when normalizedAddress is missing', () => {
    const queryClient = new QueryClient()
    queryClient.setQueryData(['householdData'], BASE_HOUSEHOLD)

    patchHouseholdAddressOnFileCache(
      queryClient,
      { status: 'valid' },
      {
        streetAddress1: '789 New St',
        city: 'Washington',
        state: 'DC',
        postalCode: '20001'
      }
    )

    const updated = queryClient.getQueryData<HouseholdData>(['householdData'])
    expect(updated?.addressOnFile?.streetAddress1).toBe('789 New St')
  })

  it('does not patch cache for non-valid responses', () => {
    const queryClient = new QueryClient()
    queryClient.setQueryData(['householdData'], BASE_HOUSEHOLD)

    patchHouseholdAddressOnFileCache(
      queryClient,
      { status: 'suggestion' },
      {
        streetAddress1: '456 Oak Avenue NW',
        city: 'Washington',
        state: 'DC',
        postalCode: '20002'
      }
    )

    const unchanged = queryClient.getQueryData<HouseholdData>(['householdData'])
    expect(unchanged?.addressOnFile?.streetAddress1).toBe('1350 Pennsylvania Ave NW')
  })
})
