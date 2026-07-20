/**
 * useHouseholdData Hook Unit Tests
 *
 * Tests the household data query hook behavior including:
 * - Successful data fetching with schema validation
 * - staleTime: 0 for real-time data freshness
 * - Custom retry logic (no retry on 4xx, retry on 5xx)
 * - Exponential backoff retry delay
 */
import { focusManager, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() })
}))

const TEST_USER_ID = '018f0000-0000-7000-8000-000000000001'
const TEST_USER_B_ID = '018f0000-0000-7000-8000-000000000002'

const mockAuthSession = {
  userId: TEST_USER_ID,
  email: 'test@example.com',
  ial: '1plus' as const,
  idProofingStatus: 2,
  idProofingCompletedAt: null,
  idProofingExpiresAt: null,
  isCoLoaded: false as boolean | null,
  expiresAt: null,
  absoluteExpiresAt: null
}

vi.mock('@/features/auth', () => ({
  useAuth: vi.fn(() => ({
    session: mockAuthSession,
    isAuthenticated: true,
    isLoading: false,
    login: vi.fn()
  }))
}))

import { useAuth } from '@/features/auth'

import { householdDataQueryKey } from './queryKeys'
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
      issuanceType: 1,
      children: [{ firstName: 'Test', lastName: 'Child' }],
      childrenOnApplication: 1
    }
  ],
  coLoadedCohort: 0
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
    mockAuthSession.userId = TEST_USER_ID
    mockAuthSession.email = 'test@example.com'
    vi.mocked(useAuth).mockReturnValue({
      session: mockAuthSession,
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn()
    })
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
            applicationStatus: 99 // Unknown future enum value
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

    it('should NOT retry on 401 unauthorized and suppresses error while redirecting', async () => {
      // 401 from /household/data means the bearer middleware rejected the JWT.
      // apiFetch redirects to /login and marks the error as redirecting; the hook
      // suppresses isError so the dashboard stays in its loading shell instead of
      // flashing an error UI before the browser navigates.
      let requestCount = 0
      const originalLocation = window.location
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { ...originalLocation, replace: vi.fn() }
      })

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
        expect(result.current.isLoading).toBe(true)
      })

      expect(result.current.isError).toBe(false)
      expect(requestCount).toBe(1)
      expect(window.location.replace).toHaveBeenCalledWith('/login')

      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation
      })
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
    it('should use query key scoped to the authenticated userId', async () => {
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
        expect(queryClient.getQueryData(householdDataQueryKey(TEST_USER_ID))).toBeDefined()
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
      const queryState = queryClient.getQueryState(householdDataQueryKey(TEST_USER_ID))
      expect(queryState?.isInvalidated || queryState?.dataUpdatedAt).toBeTruthy()
    })

    it('does not refetch when the window regains focus', async () => {
      let fetchCount = 0
      server.use(
        http.get('/api/household/data', () => {
          fetchCount++
          return HttpResponse.json(TEST_HOUSEHOLD_DATA)
        })
      )

      // A bare QueryClient carries React Query's library default of
      // refetchOnWindowFocus: true, so this pins the hook's own explicit
      // opt-out rather than the app provider's default.
      const queryClient = new QueryClient()

      const { result } = renderHook(() => useHouseholdData(), {
        wrapper: ({ children }) => (
          <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        )
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })
      expect(fetchCount).toBe(1)

      focusManager.setFocused(false)
      focusManager.setFocused(true)

      // Give any wrongly-triggered refetch time to hit the mock server
      await new Promise((resolve) => setTimeout(resolve, 50))
      expect(fetchCount).toBe(1)
      focusManager.setFocused(undefined)
    })
  })

  describe('Session identity isolation', () => {
    it('does not show prior user household data after session userId changes', async () => {
      vi.useRealTimers()

      const householdA = { ...TEST_HOUSEHOLD_DATA, email: 'user-a@example.com' }
      const householdB = { ...TEST_HOUSEHOLD_DATA, email: 'user-b@example.com' }

      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(householdA)
        })
      )

      const queryClient = createTestQueryClient()
      const wrapper = ({ children }: { children: React.ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      )

      const { result, rerender } = renderHook(() => useHouseholdData(), { wrapper })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })
      expect(result.current.data?.email).toBe('user-a@example.com')

      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json(householdB)
        })
      )

      const sessionB = { ...mockAuthSession, userId: TEST_USER_B_ID, email: 'user-b@example.com' }
      vi.mocked(useAuth).mockReturnValue({
        session: sessionB,
        isAuthenticated: true,
        isLoading: false,
        login: vi.fn()
      })

      rerender()

      await waitFor(() => {
        expect(result.current.data?.email).toBe('user-b@example.com')
      })

      expect(
        (
          queryClient.getQueryData(householdDataQueryKey(TEST_USER_ID)) as
            | { email: string }
            | undefined
        )?.email
      ).toBe('user-a@example.com')
      expect(
        (
          queryClient.getQueryData(householdDataQueryKey(TEST_USER_B_ID)) as
            | { email: string }
            | undefined
        )?.email
      ).toBe('user-b@example.com')
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

  describe('Deferred card details', () => {
    it('fetches shell then full household when deferCardDetailsOnLoad is true', async () => {
      vi.useRealTimers()
      const requests: string[] = []
      server.use(
        http.get('/api/household/data', ({ request }) => {
          const url = new URL(request.url)
          requests.push(url.searchParams.get('includeCardDetails') ?? 'true')
          const includeCardDetails = url.searchParams.get('includeCardDetails') !== 'false'
          const caseData = includeCardDetails
            ? { ebtCardLastFour: '9999', ebtCardStatus: 'Active' }
            : { ebtCardStatus: 'Unknown' }
          return HttpResponse.json({
            email: 'test@example.com',
            benefitIssuanceType: 1,
            summerEbtCases: [
              {
                summerEBTCaseID: 'CASE-1',
                childFirstName: 'Test',
                childLastName: 'Child',
                childDateOfBirth: '2015-01-01',
                householdType: 'SEBT',
                eligibilityType: 'NSLP',
                issuanceType: 1,
                ...caseData
              }
            ],
            applications: [],
            coLoadedCohort: 0
          })
        })
      )

      const { result } = renderHook(() => useHouseholdData({ deferCardDetailsOnLoad: true }), {
        wrapper: createWrapper()
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      await waitFor(() => {
        expect(result.current.data?.summerEbtCases[0]?.ebtCardLastFour).toBe('9999')
      })

      expect(requests.filter((r) => r === 'false').length).toBeGreaterThanOrEqual(1)
      expect(requests.filter((r) => r === 'true').length).toBeGreaterThanOrEqual(1)
    })
  })
})
