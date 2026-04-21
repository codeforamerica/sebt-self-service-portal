import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'

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

// Neither SignOutLink nor UserProfileCard uses useAuth anymore (logout is
// now a plain anchor to /api/auth/logout), so no auth context mock is needed.

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

  it('renders sign-out link in error state', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
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
})
