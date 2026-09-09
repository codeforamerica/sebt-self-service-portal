import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import type { RequestOtpRequest } from './schema'

async function requestOtp(data: RequestOtpRequest): Promise<void> {
  await apiFetch<void>('/auth/otp/request', {
    method: 'POST',
    body: data
  })
}

export function useRequestOtp() {
  return useMutation({
    mutationFn: requestOtp,
    retry: (failureCount, error) => {
      // Don't retry client errors (4xx) - these are validation/auth issues
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      // Retry server errors (5xx) up to 2 times
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
