/**
 * useRefreshToken Hook Unit Tests
 *
 * Tests the token refresh mutation hook behavior including:
 * - Successful token refresh
 * - Error handling for 4xx errors (no retry)
 * - Retry behavior for 5xx errors
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import { useRefreshToken } from './useRefreshToken'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  })
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useRefreshToken', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('Successful Refresh', () => {
    it('should return new token on successful refresh', async () => {
      server.use(
        http.post('/api/auth/refresh', () => {
          return HttpResponse.json({ token: 'new-jwt-token-12345' })
        })
      )

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: createWrapper()
      })

      result.current.mutate(undefined)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.data?.token).toBe('new-jwt-token-12345')
    })

    it('should validate response with Zod schema', async () => {
      server.use(
        http.post('/api/auth/refresh', () => {
          return HttpResponse.json({ token: 'valid-token' })
        })
      )

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: createWrapper()
      })

      result.current.mutate(undefined)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      // Schema requires token to be a string
      expect(typeof result.current.data?.token).toBe('string')
    })
  })

  describe('Error Handling', () => {
    it('should NOT retry on 401 unauthorized', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      // Should only make 1 request - no retries for 4xx
      expect(requestCount).toBe(1)
    })

    it('should NOT retry on 400 bad request', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(requestCount).toBe(1)
    })

    it('should NOT retry on 403 forbidden', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          return HttpResponse.json({ error: 'Forbidden' }, { status: 403 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(requestCount).toBe(1)
    })
  })

  describe('Retry Logic', () => {
    it('should retry on 5xx server errors up to 2 times', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          return HttpResponse.json({ error: 'Server Error' }, { status: 500 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      // Advance timers to allow retries with exponential backoff
      await vi.advanceTimersByTimeAsync(1000) // First retry delay
      await vi.advanceTimersByTimeAsync(2000) // Second retry delay
      await vi.advanceTimersByTimeAsync(4000) // Extra time for processing

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      // Should make 3 requests total: initial + 2 retries
      expect(requestCount).toBe(3)
    })

    it('should succeed on retry after transient 503 error', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          if (requestCount === 1) {
            return HttpResponse.json({ error: 'Service Unavailable' }, { status: 503 })
          }
          return HttpResponse.json({ token: 'recovered-token' })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      // Advance timer for retry
      await vi.advanceTimersByTimeAsync(1000)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(requestCount).toBe(2)
      expect(result.current.data?.token).toBe('recovered-token')
    })

    it('should succeed on second retry after two 502 errors', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/auth/refresh', () => {
          requestCount++
          if (requestCount <= 2) {
            return HttpResponse.json({ error: 'Bad Gateway' }, { status: 502 })
          }
          return HttpResponse.json({ token: 'final-success-token' })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(undefined)

      // Advance timers for retries
      await vi.advanceTimersByTimeAsync(1000) // First retry
      await vi.advanceTimersByTimeAsync(2000) // Second retry

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(requestCount).toBe(3)
      expect(result.current.data?.token).toBe('final-success-token')
    })
  })

  describe('Mutation Callbacks', () => {
    it('should call onSuccess callback with token data', async () => {
      server.use(
        http.post('/api/auth/refresh', () => {
          return HttpResponse.json({ token: 'callback-test-token' })
        })
      )

      const onSuccess = vi.fn()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: createWrapper()
      })

      result.current.mutate(undefined, { onSuccess })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(onSuccess).toHaveBeenCalledWith(
        { token: 'callback-test-token' },
        undefined,
        undefined,
        expect.any(Object) // TanStack Query context
      )
    })

    it('should call onError callback on failure', async () => {
      server.use(
        http.post('/api/auth/refresh', () => {
          return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
        })
      )

      const onError = vi.fn()

      const { result } = renderHook(() => useRefreshToken(), {
        wrapper: createWrapper()
      })

      result.current.mutate(undefined, { onError })

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(onError).toHaveBeenCalled()
    })
  })
})
