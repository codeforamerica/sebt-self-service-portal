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

vi.mock('@/lib/oidc-pkce', () => ({
  buildAuthorizationUrl: () => 'https://idp.example/authorize',
  generateCodeChallenge: async () => 'challenge',
  generateCodeVerifier: () => 'verifier',
  generateState: () => 'state',
  getOidcRedirectUriForCurrentOrigin: () => 'http://localhost:3000/callback',
  savePkceForCallback: vi.fn()
}))

/**
 * Routes the shared apiFetch mock to either the /auth/status response (used by AuthProvider
 * to establish the session) or the OIDC config response (used by IalGuard when the user
 * clicks Verify). Call with `ial: null` to simulate an unauthenticated user.
 */
function setupApiFetchMock(options: {
  ial: string | null
  idProofingCompletedAtSecondsAgo?: number
  oidcConfigFailure?: boolean
}) {
  apiFetchMock.mockImplementation((endpoint: string) => {
    if (endpoint === '/auth/status') {
      if (options.ial === null) {
        return Promise.reject(Object.assign(new Error('Unauthorized'), { status: 401 }))
      }
      const nowSec = Math.floor(Date.now() / 1000)
      return Promise.resolve({
        isAuthorized: true,
        email: 'user@example.com',
        ial: options.ial,
        idProofingStatus: 2,
        idProofingCompletedAt:
          options.idProofingCompletedAtSecondsAgo != null
            ? nowSec - options.idProofingCompletedAtSecondsAgo
            : nowSec,
        idProofingExpiresAt: null
      })
    }
    if (endpoint.startsWith('/auth/oidc/') && endpoint.includes('/config')) {
      if (options.oidcConfigFailure) {
        return Promise.reject(new Error('config failed'))
      }
      return Promise.resolve({
        authorizationEndpoint: 'https://idp.example/auth',
        tokenEndpoint: 'https://idp.example/token',
        clientId: 'cid',
        redirectUri: 'http://localhost:3000/callback'
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

    await waitFor(() => {
      expect(apiFetchMock).toHaveBeenCalledWith(
        expect.stringContaining('/auth/oidc/'),
        expect.anything()
      )
    })
    expect(window.location.href).toBe('https://idp.example/authorize')
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

  it('failure state shows step-up failure layout; Continue behaves like Back', async () => {
    setupApiFetchMock({ ial: '1', oidcConfigFailure: true })
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(2)

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTimeAsync })

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
      expect(screen.getByRole('button', { name: 'Verify' })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Verify' }))

    await waitFor(() => {
      expect(
        screen.getByRole('heading', {
          name: /able to show your (DC SUN Bucks|Summer EBT) information/i
        })
      ).toBeInTheDocument()
    })
    expect(screen.getByText(/contact us if you need more help/i)).toBeInTheDocument()

    mockBack.mockClear()
    await user.click(screen.getByRole('button', { name: /Return to dashboard|Continue/i }))
    expect(mockBack).toHaveBeenCalledTimes(1)
  })
})
