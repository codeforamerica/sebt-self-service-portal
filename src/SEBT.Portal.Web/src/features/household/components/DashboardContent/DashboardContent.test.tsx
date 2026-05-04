import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { TEST_HOUSEHOLD_DATA } from '@/mocks/handlers'
import { server } from '@/mocks/server'

import { DashboardContent } from './DashboardContent'

// Mock analytics to spy on setPageData / trackEvent calls.
const mockSetPageData = vi.fn()
const mockSetUserData = vi.fn()
const mockTrackEvent = vi.fn()
vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      setPageData: mockSetPageData,
      setUserData: mockSetUserData,
      trackEvent: mockTrackEvent,
      pageLoad: vi.fn(),
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserProfile: vi.fn(),
      get: vi.fn()
    })
  }
})

// Mock router, searchParams, and auth for UserProfileCard + DashboardAlerts
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn()
  }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/dashboard'
}))

// SignOutLink and UserProfileCard no longer use useAuth (logout is a plain
// anchor to /api/auth/logout); only DashboardContent itself reads useAuth
// for the co-loaded analytics branch. Preserve the real SignOutLink by
// extending the actual module instead of replacing it.
const mockAuthSession: { isCoLoaded: boolean | null } = { isCoLoaded: false }
vi.mock('@/features/auth', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth')>()
  return {
    ...actual,
    useAuth: () => ({
      session: mockAuthSession,
      logout: vi.fn()
    })
  }
})

vi.mock('@/features/feature-flags', () => ({
  useFeatureFlag: (flag: string) => {
    if (flag === 'show_contact_preferences') return true
    return false
  }
}))

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  })
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createTestQueryClient()
  return {
    ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>),
    queryClient
  }
}

describe('DashboardContent', () => {
  beforeEach(() => {
    mockSetPageData.mockClear()
    mockSetUserData.mockClear()
    mockTrackEvent.mockClear()
    mockAuthSession.isCoLoaded = false
  })

  it('shows loading skeleton initially', () => {
    renderWithProviders(<DashboardContent />)

    const loadingStatus = screen.getByRole('status')
    expect(loadingStatus).toBeInTheDocument()
    expect(loadingStatus).toHaveAttribute('aria-label', 'Loading dashboard')
  })

  it('renders household data on success', async () => {
    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      // Email is now part of "Your preferred contact" field
      expect(screen.getByText(/test@example\.com/)).toBeInTheDocument()
    })

    // Children should be rendered
    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('James Martinez')).toBeInTheDocument()
  })

  it('renders error alert on API failure', async () => {
    // 400 surfaces the error UI without retries (4xx skips the hook's retry logic).
    // 401 is suppressed by useHouseholdData because the SPA redirects to /login on
    // session-invalid responses; using it here would test the redirect path instead.
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('renders sign-out link in error state', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    expect(screen.getByRole('link', { name: /logout|sign out/i })).toBeInTheDocument()
  })

  it('renders empty state when no applications', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          ...TEST_HOUSEHOLD_DATA,
          summerEbtCases: [],
          applications: []
        })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    expect(screen.getByRole('link', { name: /apply/i })).toHaveAttribute('href', '/apply')
  })

  it('renders UserProfileCard in empty state when userProfile available', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          ...TEST_HOUSEHOLD_DATA,
          summerEbtCases: [],
          applications: []
        })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    // UserProfileCard shows user's name from the response
    expect(screen.getByText('Maria L. Martinez')).toBeInTheDocument()
  })

  it('renders empty state on 404', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Not found' }, { status: 404 })
      })
    )

    renderWithProviders(<DashboardContent />)

    // 404 triggers error state since useQuery treats it as error via ApiError
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('renders sign-out link in empty state', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          ...TEST_HOUSEHOLD_DATA,
          summerEbtCases: [],
          applications: []
        })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    expect(screen.getByRole('link', { name: /logout|sign out/i })).toBeInTheDocument()
  })

  it('renders sign-out link on 404', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Not found' }, { status: 404 })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })

    expect(screen.getByRole('link', { name: /logout|sign out/i })).toBeInTheDocument()
  })

  describe('analytics tagging when a co-loaded user lands on an empty dashboard', () => {
    it('tags household_reason="no_children" when a co-loaded user lands on an empty dashboard', async () => {
      mockAuthSession.isCoLoaded = true
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({
            ...TEST_HOUSEHOLD_DATA,
            summerEbtCases: [],
            applications: []
          })
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetPageData).toHaveBeenCalledWith('household_status', 'empty')
      expect(mockSetPageData).toHaveBeenCalledWith('household_reason', 'no_children')
      // Fire exactly once per render.
      const householdResultCalls = mockTrackEvent.mock.calls.filter(
        ([name]) => name === 'household_result'
      )
      expect(householdResultCalls).toHaveLength(1)
    })

    it('does not tag household_reason for non-co-loaded users on an empty dashboard', async () => {
      mockAuthSession.isCoLoaded = false
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({
            ...TEST_HOUSEHOLD_DATA,
            summerEbtCases: [],
            applications: []
          })
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetPageData).toHaveBeenCalledWith('household_status', 'empty')
      expect(mockSetPageData).not.toHaveBeenCalledWith('household_reason', 'no_children')
    })

    it('tags household_status="success" when co-loaded user has cases (NOT the no_children path)', async () => {
      mockAuthSession.isCoLoaded = true
      // Default TEST_HOUSEHOLD_DATA has non-empty cases.
      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetPageData).toHaveBeenCalledWith('household_status', 'success')
      expect(mockSetPageData).not.toHaveBeenCalledWith('household_reason', 'no_children')
    })
  })

  describe('co-loaded cohort analytics', () => {
    // Each test ships the backend's coLoadedCohort value and asserts the
    // standardized snake_case analytics property. The payload shape matches
    // a post-filter response — the frontend never sees co-loaded cases for
    // the excluded cohort, so these fixtures reflect that intentionally.
    function respondWith(overrides: Record<string, unknown>) {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({ ...TEST_HOUSEHOLD_DATA, ...overrides })
        })
      )
    }

    it('emits co_loaded_cohort=unknown when the API omits coLoadedCohort', async () => {
      server.use(
        http.get('/api/household/data', () => {
          const payload = { ...TEST_HOUSEHOLD_DATA } as Record<string, unknown>
          delete payload.coLoadedCohort
          return HttpResponse.json(payload)
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockSetUserData).toHaveBeenCalledWith('co_loaded_cohort', 'unknown', [
          'default',
          'analytics'
        ])
      })
    })

    it('emits co_loaded_cohort=non_co_loaded for households with no co-loaded cases', async () => {
      respondWith({ coLoadedCohort: 'NonCoLoaded' })

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockSetUserData).toHaveBeenCalledWith('co_loaded_cohort', 'non_co_loaded', [
          'default',
          'analytics'
        ])
      })
    })

    it('emits co_loaded_cohort=co_loaded_only for co-loaded-only households', async () => {
      respondWith({ coLoadedCohort: 'CoLoadedOnly' })

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockSetUserData).toHaveBeenCalledWith('co_loaded_cohort', 'co_loaded_only', [
          'default',
          'analytics'
        ])
      })
    })

    it('emits co_loaded_cohort=mixed_or_applicant_excluded for the excluded cohort', async () => {
      // Payload reflects the post-filter view: the excluded cohort's co-loaded
      // cases are suppressed upstream, so only non-co-loaded cases remain.
      respondWith({ coLoadedCohort: 'MixedOrApplicantExcluded' })

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockSetUserData).toHaveBeenCalledWith(
          'co_loaded_cohort',
          'mixed_or_applicant_excluded',
          ['default', 'analytics']
        )
      })
    })

    it('does not emit a cohort property when the API returns an error', async () => {
      // 400 surfaces the error UI without retries (4xx skips the hook's retry logic).
      // 401 is suppressed by useHouseholdData while the SPA redirects to /login, so it
      // would leave the component in `isLoading` and never run the error-analytics branch.
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockSetPageData).toHaveBeenCalledWith('household_status', 'error')
      })
      expect(mockSetUserData).not.toHaveBeenCalledWith(
        'co_loaded_cohort',
        expect.anything(),
        expect.anything()
      )
    })
  })

  describe('coloading_status / household_type tagging (DC-215)', () => {
    it('tags non_co_loaded when session.isCoLoaded is false', async () => {
      mockAuthSession.isCoLoaded = false
      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetUserData).toHaveBeenCalledWith('coloading_status', 'non_co_loaded', [
        'default',
        'analytics'
      ])
      expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'non_co_loaded')
    })

    it('tags mixed_eligibility when co-loaded user also has SummerEbt cases', async () => {
      // Default TEST_HOUSEHOLD_DATA has SummerEbt cases (issuanceType: 1).
      mockAuthSession.isCoLoaded = true
      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetUserData).toHaveBeenCalledWith('coloading_status', 'mixed_eligibility', [
        'default',
        'analytics'
      ])
      expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'mixed_eligibility')
    })

    it('tags co_loaded_only when co-loaded user has only SnapEbtCard/TanfEbtCard cases and no applications', async () => {
      mockAuthSession.isCoLoaded = true
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({
            ...TEST_HOUSEHOLD_DATA,
            summerEbtCases: [
              { ...TEST_HOUSEHOLD_DATA.summerEbtCases[0], issuanceType: 'SnapEbtCard' }
            ],
            applications: []
          })
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('household_result')
      })

      expect(mockSetUserData).toHaveBeenCalledWith('coloading_status', 'co_loaded_only', [
        'default',
        'analytics'
      ])
      expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'co_loaded_only')
    })
  })
})
