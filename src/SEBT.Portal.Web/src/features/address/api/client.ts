'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api/client'

import type { AddressUpdateResponse, UpdateAddressRequest } from './schema'
import { AddressUpdateResponseSchema } from './schema'

const ADDRESS_ENDPOINT = '/household/address'

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
    onSuccess: async (result) => {
      if (result.status === 'valid') {
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
