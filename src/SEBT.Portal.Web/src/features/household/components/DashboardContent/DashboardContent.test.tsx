import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { TEST_HOUSEHOLD_DATA } from '@/mocks/handlers'
import { server } from '@/mocks/server'

import { DashboardContent } from './DashboardContent'

// Mock router, searchParams, and auth for UserProfileCard + DashboardAlerts
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn()
  }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/dashboard'
}))

vi.mock('@/features/auth', () => ({
  useAuth: () => ({
    logout: vi.fn()
  })
}))

const setPageDataSpy = vi.fn()
const setUserDataSpy = vi.fn()
const trackEventSpy = vi.fn()

vi.mock('@sebt/analytics', async () => {
  const actual = await vi.importActual<typeof import('@sebt/analytics')>('@sebt/analytics')
  return {
    ...actual,
    useDataLayer: () => ({
      setPageData: setPageDataSpy,
      setUserData: setUserDataSpy,
      trackEvent: trackEventSpy,
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserProfile: vi.fn(),
      pageLoad: vi.fn(),
      get: vi.fn()
    })
  }
})

beforeEach(() => {
  setPageDataSpy.mockClear()
  setUserDataSpy.mockClear()
  trackEventSpy.mockClear()
})

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
    // Use 401 to avoid hook retry logic (4xx errors are not retried)
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
      })
    )

    renderWithProviders(<DashboardContent />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
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

    expect(screen.getByRole('link')).toHaveAttribute('href', '/apply')
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

    it('emits co_loaded_cohort=non_co_loaded for households with no co-loaded cases', async () => {
      respondWith({ coLoadedCohort: 'NonCoLoaded' })

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(setUserDataSpy).toHaveBeenCalledWith('co_loaded_cohort', 'non_co_loaded', [
          'default',
          'analytics'
        ])
      })
    })

    it('emits co_loaded_cohort=co_loaded_only for co-loaded-only households', async () => {
      respondWith({ coLoadedCohort: 'CoLoadedOnly' })

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(setUserDataSpy).toHaveBeenCalledWith('co_loaded_cohort', 'co_loaded_only', [
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
        expect(setUserDataSpy).toHaveBeenCalledWith(
          'co_loaded_cohort',
          'mixed_or_applicant_excluded',
          ['default', 'analytics']
        )
      })
    })

    it('does not emit a cohort property when the API returns an error', async () => {
      server.use(
        http.get('/api/household/data', () => {
          return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
        })
      )

      renderWithProviders(<DashboardContent />)

      await waitFor(() => {
        expect(setPageDataSpy).toHaveBeenCalledWith('household_status', 'error')
      })
      expect(setUserDataSpy).not.toHaveBeenCalledWith(
        'co_loaded_cohort',
        expect.anything(),
        expect.anything()
      )
    })
  })
})
