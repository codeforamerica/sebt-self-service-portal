import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import type { ValidateOtpRequest } from './schema'

async function validateOtp(data: ValidateOtpRequest): Promise<void> {
  await apiFetch<void>('/auth/otp/validate', {
    method: 'POST',
    body: data
  })
}

export function useValidateOtp() {
  return useMutation({
    mutationFn: validateOtp,
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
