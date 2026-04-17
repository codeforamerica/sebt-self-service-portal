import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from '@/features/auth/context'

import { IalGuard } from './IalGuard'

const mockBack = vi.fn()
const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    back: mockBack,
    push: mockPush,
    replace: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn()
  })
}))

const apiFetchMock = vi.fn()
vi.mock('@/api', () => ({
  apiFetch: (...args: unknown[]) => apiFetchMock(...args),
  ApiError: class ApiError extends Error {
    status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  }
}))

vi.mock('@/api/client', () => ({
  apiFetch: (...args: unknown[]) => apiFetchMock(...args),
  ApiError: class ApiError extends Error {
    status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  }
}))

/**
 * Sets up the apiFetch mock for /auth/status (used by AuthProvider to establish the session).
 * IalGuard no longer calls apiFetch directly — it navigates to the server-side authorize
 * endpoint instead. Call with `ial: null` to simulate an unauthenticated user.
 */
function setupApiFetchMock(options: {
  ial: string | null
  idProofingCompletedAtSecondsAgo?: number
}) {
  apiFetchMock.mockImplementation((endpoint: string) => {
    if (endpoint === '/auth/status') {
      if (options.ial === null) {
        return Promise.reject(Object.assign(new Error('Unauthorized'), { status: 401 }))
      }
      const nowSec = Math.floor(Date.now() / 1000)
      const fiveYearsInSeconds = 5 * 365.25 * 24 * 60 * 60
      return Promise.resolve({
        isAuthorized: true,
        email: 'user@example.com',
        ial: options.ial,
        idProofingStatus: 2,
        idProofingCompletedAt:
          options.idProofingCompletedAtSecondsAgo != null
            ? nowSec - options.idProofingCompletedAtSecondsAgo
            : nowSec,
        idProofingExpiresAt: nowSec + fiveYearsInSeconds
      })
    }
    return Promise.resolve({})
  })
}

describe('IalGuard', () => {
  let prevNextPublicState: string | undefined

  beforeEach(() => {
    prevNextPublicState = process.env.NEXT_PUBLIC_STATE
    process.env.NEXT_PUBLIC_STATE = 'co'
    vi.useFakeTimers({ shouldAdvanceTime: true })
    apiFetchMock.mockReset()
    mockBack.mockReset()
    mockPush.mockReset()
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { href: '', origin: 'http://localhost:3000' }
    })
  })

  afterEach(() => {
    vi.useRealTimers()
    if (prevNextPublicState === undefined) {
      delete process.env.NEXT_PUBLIC_STATE
    } else {
      process.env.NEXT_PUBLIC_STATE = prevNextPublicState
    }
  })

  it('renders children when session already satisfies IAL gate', async () => {
    setupApiFetchMock({ ial: '1plus' })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    expect(await screen.findByText('Protected')).toBeInTheDocument()
    expect(screen.queryByText(/Please wait/)).not.toBeInTheDocument()
  })

  it('shows checking then challenge; Verify starts step-up redirect', async () => {
    setupApiFetchMock({ ial: '1' })

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTimeAsync })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    await waitFor(() => {
      expect(screen.getByText(/Please wait/)).toBeInTheDocument()
    })
    expect(screen.getByText(/Do not exit the page/i)).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
    })

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /confirm it/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Verify' }))

    // IalGuard navigates directly to the server-side authorize endpoint (V04 fix).
    expect(window.location.href).toContain('/api/auth/oidc/co/authorize')
    expect(window.location.href).toContain('stepUp=true')
  })

  it('Back uses router.back when history length > 1', async () => {
    setupApiFetchMock({ ial: '1' })
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(2)

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByText(/Please wait/)).toBeInTheDocument())
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
    })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Back' })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(mockBack).toHaveBeenCalledTimes(1)
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('Back falls back to dashboard when history length is 1', async () => {
    setupApiFetchMock({ ial: '1' })
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(1)

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    await waitFor(() => expect(screen.getByText(/Please wait/)).toBeInTheDocument())
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
    })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Back' })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(mockPush).toHaveBeenCalledWith('/dashboard')
  })
})
