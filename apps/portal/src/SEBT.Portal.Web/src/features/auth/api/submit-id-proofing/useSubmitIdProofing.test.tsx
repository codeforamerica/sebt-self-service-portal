/**
 * useSubmitIdProofing Hook Unit Tests
 *
 * Tests the id-proofing mutation hook behavior including:
 * - Successful submission returns typed response with result
 * - Correct payload sent to the endpoint
 * - Error handling for 4xx errors (no retry)
 * - Retry behavior for 5xx errors
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import type { SubmitIdProofingRequest } from './schema'
import { useSubmitIdProofing } from './useSubmitIdProofing'

// Obviously fake PII values per CLAUDE.md PII conventions
const VALID_PAYLOAD: SubmitIdProofingRequest = {
  dateOfBirth: { month: '01', day: '15', year: '1990' },
  idType: 'ssn',
  idValue: '999999999'
}

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

describe('useSubmitIdProofing', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('Successful Submission', () => {
    it('should succeed on valid id-proofing submission', async () => {
      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: createWrapper()
      })

      result.current.mutate(VALID_PAYLOAD)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })
    })

    it('should send the correct payload to the endpoint', async () => {
      let capturedBody: SubmitIdProofingRequest | null = null

      server.use(
        http.post('/api/id-proofing', async ({ request }) => {
          capturedBody = (await request.json()) as SubmitIdProofingRequest
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: createWrapper()
      })

      result.current.mutate(VALID_PAYLOAD)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(capturedBody).toEqual(VALID_PAYLOAD)
    })

    it('should return matched result on successful id proofing', async () => {
      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: createWrapper()
      })

      result.current.mutate(VALID_PAYLOAD)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.data).toEqual({ result: 'matched' })
    })

    it('should return failed result with canApply when id proofing fails', async () => {
      const failedPayload: SubmitIdProofingRequest = {
        dateOfBirth: { month: '01', day: '15', year: '1990' },
        idType: null,
        idValue: null
      }

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: createWrapper()
      })

      result.current.mutate(failedPayload)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.data).toEqual({
        result: 'failed',
        canApply: true,
        offboardingReason: 'noIdProvided'
      })
    })
  })

  describe('Contract enforcement', () => {
    it('should error when backend returns 204 instead of JSON', async () => {
      server.use(
        http.post('/api/id-proofing', () => {
          return new HttpResponse(null, { status: 204 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(VALID_PAYLOAD)

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(result.current.error?.message).toMatch(/expected a json response/i)
    })
  })

  describe('Error Handling', () => {
    it('should NOT retry on 400 bad request', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/id-proofing', () => {
          requestCount++
          return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(VALID_PAYLOAD)

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      // Should only make 1 request - no retries for 4xx
      expect(requestCount).toBe(1)
    })

    it('should NOT retry on 401 unauthorized', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/id-proofing', () => {
          requestCount++
          return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(VALID_PAYLOAD)

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
        http.post('/api/id-proofing', () => {
          requestCount++
          return HttpResponse.json({ error: 'Server Error' }, { status: 500 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(VALID_PAYLOAD)

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

    it('should succeed on retry after transient server error', async () => {
      let requestCount = 0

      server.use(
        http.post('/api/id-proofing', () => {
          requestCount++
          if (requestCount === 1) {
            return HttpResponse.json({ error: 'Service Unavailable' }, { status: 503 })
          }
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useSubmitIdProofing(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      result.current.mutate(VALID_PAYLOAD)

      // Advance timer for retry
      await vi.advanceTimersByTimeAsync(1000)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(requestCount).toBe(2)
    })
  })
})
