'use client'

import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api/client'
import type { Address, HouseholdData } from '@/features/household/api'
import { HOUSEHOLD_DATA_QUERY_KEY_PREFIX } from '@/features/household/api/queryKeys'

import type { AddressResponse, AddressUpdateResponse, UpdateAddressRequest } from './schema'
import { AddressUpdateResponseSchema } from './schema'

const ADDRESS_ENDPOINT = '/household/address'

function addressResponseToOnFile(address: AddressResponse | null | undefined): Address | undefined {
  if (!address?.streetAddress1 || !address.city || !address.state || !address.postalCode) {
    return undefined
  }

  return {
    streetAddress1: address.streetAddress1,
    streetAddress2: address.streetAddress2 ?? null,
    city: address.city,
    state: address.state,
    postalCode: address.postalCode
  }
}

function updateRequestToOnFile(request: UpdateAddressRequest): Address {
  return {
    streetAddress1: request.streetAddress1,
    streetAddress2: request.streetAddress2 ?? null,
    city: request.city,
    state: request.state,
    postalCode: request.postalCode
  }
}

/** Keeps dashboard/profile in sync immediately after a persisted address update. */
export function patchHouseholdAddressOnFileCache(
  queryClient: QueryClient,
  result: AddressUpdateResponse,
  submittedAddress: UpdateAddressRequest
): void {
  if (result.status !== 'valid') {
    return
  }

  const addressOnFile =
    addressResponseToOnFile(result.normalizedAddress) ?? updateRequestToOnFile(submittedAddress)

  const patch = (existing: HouseholdData | undefined) =>
    existing ? { ...existing, addressOnFile } : existing

  queryClient.setQueriesData<HouseholdData>({ queryKey: HOUSEHOLD_DATA_QUERY_KEY_PREFIX }, patch)
}

async function updateAddress(data: UpdateAddressRequest): Promise<AddressUpdateResponse> {
  try {
    return await apiFetch<AddressUpdateResponse>(ADDRESS_ENDPOINT, {
      method: 'PUT',
      body: data,
      schema: AddressUpdateResponseSchema
    })
  } catch (err) {
    // 422 carries a structured validation response (blocked, suggestion, too_long).
    // Parse and return it instead of throwing so the form can route to the right screen.
    if (err instanceof ApiError && err.status === 422) {
      const parsed = AddressUpdateResponseSchema.safeParse(err.data)
      if (parsed.success) {
        return parsed.data
      }
    }
    throw err
  }
}

export function useUpdateAddress() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: updateAddress,
    onSuccess: async (result, variables) => {
      if (result.status === 'valid') {
        patchHouseholdAddressOnFileCache(queryClient, result, variables)
        await queryClient.invalidateQueries({ queryKey: ['householdData'] })
      }
    },
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
