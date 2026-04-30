'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api/client'

import type { RequestCardReplacementRequest } from './schema'

const CARD_REPLACEMENT_ENDPOINT = '/household/cards/replace'

async function requestCardReplacement(data: RequestCardReplacementRequest): Promise<void> {
  await apiFetch<void>(CARD_REPLACEMENT_ENDPOINT, {
    method: 'POST',
    body: data
  })
}

export function useRequestCardReplacement() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: requestCardReplacement,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['householdData'] })
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
