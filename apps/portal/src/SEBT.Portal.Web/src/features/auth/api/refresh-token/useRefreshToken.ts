import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

/**
 * Hook to refresh the JWT session cookie.
 * Requires an existing valid session cookie. Backend responds with 204 and a fresh Set-Cookie;
 * the caller should re-read session state (e.g. via useAuth().login()) to pick up updated claims.
 */
export function useRefreshToken() {
  return useMutation({
    mutationFn: () =>
      apiFetch<void>('/auth/refresh', {
        method: 'POST'
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
