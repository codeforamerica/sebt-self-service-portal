'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api/client'

import type { AddressUpdateResponse, UpdateAddressRequest } from './schema'

const ADDRESS_ENDPOINT = '/household/address'

async function updateAddress(data: UpdateAddressRequest): Promise<AddressUpdateResponse> {
  try {
    return await apiFetch<AddressUpdateResponse>(ADDRESS_ENDPOINT, {
      method: 'PUT',
      body: data
    })
  } catch (error) {
    if (error instanceof ApiError && error.status === 422) {
      // 422 is a validation result (invalid/suggestion), not a request failure
      return error.data as unknown as AddressUpdateResponse
    }
    throw error
  }
}

export function useUpdateAddress() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: updateAddress,
    onSuccess: (result) => {
      if (result?.status === 'valid') {
        queryClient.invalidateQueries({ queryKey: ['householdData'] })
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
