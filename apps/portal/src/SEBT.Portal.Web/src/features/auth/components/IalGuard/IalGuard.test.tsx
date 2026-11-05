import type { LoginMode, StateCode } from '@sebt/design-system'
import { i18n } from '@sebt/design-system/client'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'

import enCoStepUpProcessing from '@/content/locales/en/co/step-upProcessing.json'
import enCoStepUpDisclaimer from '@/content/locales/en/co/stepUpDisclaimer.json'
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

// IalGuard decides on the login mode, not the state code; the state attribute set in
// beforeEach is still needed for the authorize URL assertion.
let mockLoginMode: LoginMode = 'oidc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getStateConfig: (state: StateCode) => ({
      ...actual.getStateConfig(state),
      loginMode: mockLoginMode
    })
  }
})

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
  // The OIDC step-up copy lives in the CO bundles; tests boot under the DC locale, so load them here.
  beforeAll(() => {
    i18n.addResourceBundle('en', 'stepUpDisclaimer', enCoStepUpDisclaimer, true, true)
    i18n.addResourceBundle('en', 'step-upProcessing', enCoStepUpProcessing, true, true)
  })

  afterAll(() => {
    i18n.removeResourceBundle('en', 'stepUpDisclaimer')
    i18n.removeResourceBundle('en', 'step-upProcessing')
  })

  beforeEach(() => {
    // IalGuard is a client component, so it reads the state from the attribute the
    // server stamped on <html> — the same path the browser takes at runtime.
    document.documentElement.dataset.state = 'co'
    mockLoginMode = 'oidc'
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
    delete document.documentElement.dataset.state
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

  it('renders children without a challenge when the login mode is OTP', async () => {
    mockLoginMode = 'otp'
    setupApiFetchMock({ ial: '1' })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    expect(await screen.findByText('Protected')).toBeInTheDocument()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })
    expect(screen.queryByText(/Please wait/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Verify' })).not.toBeInTheDocument()
    expect(window.location.href).not.toContain('/api/auth/oidc/')
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

  it('challenge heading resolves stepUpDisclaimer.title, not a hardcoded fallback', async () => {
    setupApiFetchMock({ ial: '1' })

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
      expect(screen.getByRole('heading', { name: enCoStepUpDisclaimer.title })).toBeInTheDocument()
    })
  })

  it('checking copy resolves step-upProcessing.title and .body, not hardcoded fallbacks', async () => {
    setupApiFetchMock({ ial: '1' })

    render(
      <AuthProvider>
        <IalGuard>
          <p>Protected</p>
        </IalGuard>
      </AuthProvider>
    )

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: enCoStepUpProcessing.title })).toBeInTheDocument()
    })
    expect(screen.getByText(enCoStepUpProcessing.body)).toBeInTheDocument()
  })

  it('Back navigates to dashboard', async () => {
    setupApiFetchMock({ ial: '1' })

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
    expect(mockBack).not.toHaveBeenCalled()
  })
})
