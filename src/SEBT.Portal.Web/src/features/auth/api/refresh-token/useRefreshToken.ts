import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import type { ValidateOtpResponse } from '../validate-otp/schema'
import { ValidateOtpResponseSchema } from '../validate-otp/schema'

/**
 * Hook to refresh the JWT token.
 * Requires a valid current token (Authorization header).
 * Returns a new token with updated claims and extended expiration.
 */
export function useRefreshToken() {
  return useMutation({
    mutationFn: () =>
      apiFetch<ValidateOtpResponse>('/auth/refresh', {
        method: 'POST',
        schema: ValidateOtpResponseSchema
      }),
    // Don't retry on 4xx errors (auth issues)
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 2
    }
  })
}
