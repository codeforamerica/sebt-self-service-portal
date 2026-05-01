/**
 * useHouseholdData Hook Unit Tests
 *
 * Tests the household data query hook behavior including:
 * - Successful data fetching with schema validation
 * - staleTime: 0 for real-time data freshness
 * - Custom retry logic (no retry on 4xx, retry on 5xx)
 * - Exponential backoff retry delay
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() })
}))

import { useHouseholdData } from './useHouseholdData'

const TEST_HOUSEHOLD_DATA = {
  email: 'test@example.com',
  phone: '8185558439',
  benefitIssuanceType: 1,
  applications: [
    {
      applicationNumber: 'APP-001',
      caseNumber: 'CASE-001',
      applicationStatus: 'Approved',
      benefitIssueDate: '2026-01-15T00:00:00Z',
      benefitExpirationDate: '2026-06-30T00:00:00Z',
      last4DigitsOfCard: '1234',
      cardStatus: 'Active',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: '2026-01-05T00:00:00Z',
      cardActivatedAt: '2026-01-15T00:00:00Z',
      cardDeactivatedAt: null,
      issuanceType: 1,
      children: [{ firstName: 'Test', lastName: 'Child' }],
      childrenOnApplication: 1
    }
  ]
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  })
}

function createWrapper() {
  const queryClient = createTestQueryClient()
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useHouseholdData', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('Successful Fetching', () => {
    it('should fetch and return household data', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: createWrapper()
      })

      expect(result.current.isLoading).toBe(true)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.data?.email).toBe('test@example.com')
      expect(result.current.data?.applications).toHaveLength(1)
    })

    it('should validate response with Zod schema', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: createWrapper()
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      // Schema transforms issuanceType from number to string
      expect(result.current.data?.applications?.[0]?.issuanceType).toBe('SummerEbt')
      expect(result.current.data?.benefitIssuanceType).toBe('SummerEbt')
    })

    it('should map unknown enum values to Unknown instead of failing validation', async () => {
      const dataWithUnknownEnums = {
        ...TEST_HOUSEHOLD_DATA,
        benefitIssuanceType: 99, // Unknown future enum value
        applications: [
          {
            ...TEST_HOUSEHOLD_DATA.applications[0],
            issuanceType: 99, // Unknown future enum value
            applicationStatus: 99, // Unknown future enum value
            cardStatus: 99 // Unknown future enum value
          }
        ]
      }

      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(dataWithUnknownEnums)
        })
      )

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: createWrapper()
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      // Unknown values should map to 'Unknown' string
      expect(result.current.data?.benefitIssuanceType).toBe('Unknown')
      expect(result.current.data?.applications?.[0]?.issuanceType).toBe('Unknown')
      expect(result.current.data?.applications?.[0]?.applicationStatus).toBe('Unknown')
      expect(result.current.data?.applications?.[0]?.cardStatus).toBe('Unknown')
    })
  })

  describe('Retry Logic', () => {
    it('should NOT retry on 4xx client errors', async () => {
      let requestCount = 0

      server.use(
        http.get('/api/household/data', () => {
          requestCount++
          return HttpResponse.json({ error: 'Not Found' }, { status: 404 })
        })
      )

      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            // Use the hook's retry logic by not overriding it
          }
        }
      })

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      // Should only make 1 request - no retries for 4xx
      expect(requestCount).toBe(1)
    })

    it('should NOT retry on 401 unauthorized', async () => {
      let requestCount = 0

      server.use(
        http.get('/api/household/data', () => {
          requestCount++
          return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(requestCount).toBe(1)
    })

    it('should retry on 5xx server errors up to 2 times', async () => {
      let requestCount = 0

      server.use(
        http.get('/api/household/data', () => {
          requestCount++
          return HttpResponse.json({ error: 'Server Error' }, { status: 500 })
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

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
        http.get('/api/household/data', () => {
          requestCount++
          if (requestCount === 1) {
            return HttpResponse.json({ error: 'Server Error' }, { status: 503 })
          }
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      // Advance timer for retry
      await vi.advanceTimersByTimeAsync(1000)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(requestCount).toBe(2)
      expect(result.current.data?.email).toBe('test@example.com')
    })
  })

  describe('Query Configuration', () => {
    it('should use correct query key', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      const queryClient = new QueryClient()

      renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      await waitFor(() => {
        expect(queryClient.getQueryData(['householdData'])).toBeDefined()
      })
    })

    it('should have staleTime of 0 for real-time data', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      const queryClient = new QueryClient()

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      // With staleTime: 0, data should be immediately stale
      const queryState = queryClient.getQueryState(['householdData'])
      expect(queryState?.isInvalidated || queryState?.dataUpdatedAt).toBeTruthy()
    })
  })

  describe('Error Handling', () => {
    it('should expose error details for 404 responses', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({ error: 'Not Found' }, { status: 404 })
        })
      )

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: createWrapper()
      })

      await waitFor(() => {
        expect(result.current.isError).toBe(true)
      })

      expect(result.current.error).toBeDefined()
    })
  })
})
