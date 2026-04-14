import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import type React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import { CoLoadedInfo } from './CoLoadedInfo'

const mockPush = vi.fn()
const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: mockBack
  })
}))

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  })
}

function renderCoLoadedInfo(props: React.ComponentProps<typeof CoLoadedInfo> = {}) {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <CoLoadedInfo {...props} />
      </QueryClientProvider>
    )
  }
}

describe('CoLoadedInfo', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
  })

  // --- Content rendering ---

  it('renders FIS phone callout', async () => {
    renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByText(/\(888\) 304-9167/)).toBeInTheDocument()
    })
  })

  it('renders address on file from household data', async () => {
    renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByText('1350 Pennsylvania Ave NW')).toBeInTheDocument()
      expect(screen.getByText('Suite 400')).toBeInTheDocument()
      expect(screen.getByText(/Washington, DC 20004/)).toBeInTheDocument()
    })
  })

  it('does not render address block when addressOnFile is null', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'test@example.com',
          phone: '3035550100',
          applications: [],
          addressOnFile: null,
          userProfile: { firstName: 'Maria', middleName: 'L', lastName: 'Martinez' }
        })
      })
    )

    renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByText(/\(888\) 304-9167/)).toBeInTheDocument()
    })

    expect(screen.queryByText('1350 Pennsylvania Ave NW')).not.toBeInTheDocument()
  })

  it('renders keep-your-card message', async () => {
    renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByText(/keep your card for next year/i)).toBeInTheDocument()
    })
  })

  it('renders tap-to-call link with tel: href', async () => {
    renderCoLoadedInfo()

    await waitFor(() => {
      const link = screen.getByRole('link', { name: /tap to call/i })
      expect(link).toHaveAttribute('href', 'tel:+18883049167')
    })
  })

  // --- Navigation ---

  it('navigates back when back button is clicked', async () => {
    const { user } = renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })

  it('navigates to dashboard when continue button is clicked', async () => {
    const { user } = renderCoLoadedInfo()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/dashboard')
  })

  // --- Terminal mode ---
  // When used as a terminal info page (e.g. denied-user redirect to
  // /profile/address/info), the component has no next step. Render a single
  // "Back to dashboard" action instead of the Back + Continue pair.

  it('renders only a single dashboard action when terminal is true', async () => {
    renderCoLoadedInfo({ terminal: true })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /return to dashboard/i })).toBeInTheDocument()
    })

    expect(screen.queryByRole('button', { name: /^back$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /continue/i })).not.toBeInTheDocument()
  })

  it('navigates to /dashboard when terminal dashboard button is clicked', async () => {
    const { user } = renderCoLoadedInfo({ terminal: true })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /return to dashboard/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /return to dashboard/i }))
    expect(mockPush).toHaveBeenCalledWith('/dashboard')
  })
})
