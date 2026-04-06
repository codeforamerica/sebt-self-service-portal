import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
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

function renderCoLoadedInfo() {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <CoLoadedInfo />
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
      expect(screen.getByText('123 Main Street')).toBeInTheDocument()
      expect(screen.getByText('Apt 4B')).toBeInTheDocument()
      expect(screen.getByText(/Washington, DC 20001/)).toBeInTheDocument()
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

    expect(screen.queryByText('123 Main Street')).not.toBeInTheDocument()
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
})
