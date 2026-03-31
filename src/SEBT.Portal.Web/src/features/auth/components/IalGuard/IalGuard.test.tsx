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
  apiFetch: (...args: unknown[]) => apiFetchMock(...args)
}))

vi.mock('@/lib/oidc-pkce', () => ({
  buildAuthorizationUrl: () => 'https://idp.example/authorize',
  generateCodeChallenge: async () => 'challenge',
  generateCodeVerifier: () => 'verifier',
  generateState: () => 'state',
  getOidcRedirectUriForCurrentOrigin: () => 'http://localhost:3000/callback',
  savePkceForCallback: vi.fn()
}))

function base64UrlEncodeJson(obj: Record<string, unknown>): string {
  const json = JSON.stringify(obj)
  return btoa(json).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** JWT that does not satisfy IAL1+ step-up gate (IAL 1 + fresh id_proofing for completeness). */
function buildLowIalToken(): string {
  const nowSec = Math.floor(Date.now() / 1000)
  const payload = base64UrlEncodeJson({
    ial: '1',
    id_proofing_completed_at: nowSec
  })
  return `h.${payload}.s`
}

function buildPassingToken(): string {
  const nowSec = Math.floor(Date.now() / 1000)
  const payload = base64UrlEncodeJson({
    ial: '1plus',
    id_proofing_completed_at: nowSec
  })
  return `h.${payload}.s`
}

describe('IalGuard', () => {
  let prevNextPublicState: string | undefined

  beforeEach(() => {
    prevNextPublicState = process.env.NEXT_PUBLIC_STATE
    process.env.NEXT_PUBLIC_STATE = 'co'
    vi.useFakeTimers({ shouldAdvanceTime: true })
    sessionStorage.clear()
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
    sessionStorage.clear()
    if (prevNextPublicState === undefined) {
      delete process.env.NEXT_PUBLIC_STATE
    } else {
      process.env.NEXT_PUBLIC_STATE = prevNextPublicState
    }
  })

  it('renders children when JWT already satisfies IAL gate', async () => {
    sessionStorage.setItem('auth_token', buildPassingToken())

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
    sessionStorage.setItem('auth_token', buildLowIalToken())
    apiFetchMock.mockResolvedValue({
      authorizationEndpoint: 'https://idp.example/auth',
      tokenEndpoint: 'https://idp.example/token',
      clientId: 'cid',
      redirectUri: 'http://localhost:3000/callback'
    })

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTimeAsync })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    expect(screen.getByText(/Please wait/)).toBeInTheDocument()
    expect(screen.getByText(/Do not exit the page/i)).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
    })

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /confirm it/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Verify' }))

    await waitFor(() => {
      expect(apiFetchMock).toHaveBeenCalled()
    })
    expect(window.location.href).toBe('https://idp.example/authorize')
  })

  it('Back uses router.back when history length > 1', async () => {
    sessionStorage.setItem('auth_token', buildLowIalToken())
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(2)

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

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
    sessionStorage.setItem('auth_token', buildLowIalToken())
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(1)

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

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
    sessionStorage.setItem('auth_token', buildLowIalToken())
    apiFetchMock.mockRejectedValue(new Error('config failed'))
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(2)

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTimeAsync })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500)
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
