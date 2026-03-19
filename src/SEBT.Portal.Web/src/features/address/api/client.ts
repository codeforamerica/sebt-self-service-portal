'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api/client'

import type { UpdateAddressRequest } from './schema'

const ADDRESS_ENDPOINT = '/household/address'

// TODO: When state connector persistence is wired up, this may return a response
// body (e.g., canonical address). Update return type and MSW handler to match.
async function updateAddress(data: UpdateAddressRequest): Promise<void> {
  await apiFetch<void>(ADDRESS_ENDPOINT, {
    method: 'PUT',
    body: data
  })
}

export function useUpdateAddress() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: updateAddress,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['householdData'] })
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
