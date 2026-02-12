import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { TEST_HOUSEHOLD_DATA } from '@/mocks/handlers'
import { server } from '@/mocks/server'

import { DashboardContent } from './DashboardContent'

// Mock router and auth for UserProfileCard
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn()
  })
}))

vi.mock('@/features/auth', () => ({
  useAuth: () => ({
    logout: vi.fn()
  })
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

  it('renders empty state when no applications', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          ...TEST_HOUSEHOLD_DATA,
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
})
