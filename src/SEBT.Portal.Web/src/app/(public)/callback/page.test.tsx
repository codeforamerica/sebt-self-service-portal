/**
 * OIDC Callback Page Unit Tests
 *
 * Tests the OIDC callback flow including:
 * - Successful token exchange and redirect to dashboard
 * - Missing code/state parameters
 * - Exchange-code failure
 * - IdP error redirect (?error=)
 *
 * PKCE/sessionStorage validation tests have been removed — all flow metadata
 * (stateCode, isStepUp, returnUrl, state validation) is now handled server-side
 * via the pre-auth session (V04 fix).
 */
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

// Mock router
const mockReplace = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace,
    push: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn()
  })
}))

// Mock @/features/auth without loading the barrel (barrel pulls IalGuard → @/env and breaks Vitest).
const { mockLogin } = vi.hoisted(() => ({ mockLogin: vi.fn() }))
vi.mock('@/features/auth', async () => {
  const api = await vi.importActual<typeof import('@/features/auth/api')>('@/features/auth/api')
  return {
    ...api,
    useAuth: () => ({
      login: mockLogin,
      logout: vi.fn(),
      isAuthenticated: false,
      session: null,
      isLoading: false
    })
  }
})

// Mock translations
// CallbackPage now uses the client-side useTranslation() hook (DC-187 fix).
const TEST_TRANSLATIONS: Record<string, Record<string, string>> = {
  login: {
    callbackSigningIn: 'Signing you in…',
    callbackSignInIssue: 'Sign-in issue',
    callbackErrorMissingParams: 'Missing sign-in information.',
    callbackErrorGeneric: 'Something went wrong.',
    callbackErrorIdpRedirect: 'Primary MyColorado sign-in did not finish.',
    callbackStepUpDeclinedTitle: 'Identity verification was not completed',
    callbackStepUpDeclinedBody:
      'You chose not to share information with our identity verification partner.',
    callbackStepUpDeclinedActionDashboard: 'Go to dashboard'
  }
}

vi.mock('react-i18next', () => ({
  useTranslation: (namespace: string) => ({
    /* eslint-disable security/detect-object-injection -- test mock; namespace + key controlled */
    t: (key: string, defaultValue?: string) =>
      TEST_TRANSLATIONS[namespace]?.[key] ?? defaultValue ?? key,
    /* eslint-enable security/detect-object-injection */
    i18n: { language: 'en' }
  })
}))

// Mock state
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => 'co'
  }
})

import CallbackPage from './page'

describe('CallbackPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // Default: URL has code and state
    Object.defineProperty(window, 'location', {
      value: {
        search: '?code=test-auth-code&state=test-state-value',
        href: 'http://localhost:3000/callback?code=test-auth-code&state=test-state-value'
      },
      writable: true
    })
  })

  describe('missing URL parameters', () => {
    it('shows error when code is missing from URL', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?state=test-state',
          href: 'http://localhost:3000/callback?state=test-state'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })
    })

    it('shows error when state is missing from URL', async () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?code=test-code', href: 'http://localhost:3000/callback?code=test-code' },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })
    })
  })

  describe('successful flow', () => {
    beforeEach(() => {
      // callback returns callbackToken, complete-login sets cookie and returns empty body
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ callbackToken: 'mock-callback-token-for-testing' })
        }),
        http.post('/api/auth/oidc/complete-login', () => {
          return HttpResponse.json({})
        })
      )
    })

    it('shows the CO loading interstitial initially', () => {
      render(<CallbackPage />)
      const status = screen.getByRole('status')
      expect(status).toHaveAttribute('aria-busy', 'true')
      expect(screen.getByText('Please wait...')).toBeInTheDocument()
    })

    it('redirects to dashboard on successful login', async () => {
      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/dashboard')
      })
      expect(mockLogin).toHaveBeenCalledWith()
    })

    it('redirects to returnUrl when complete-login returns one', async () => {
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ callbackToken: 'mock-callback-token' })
        }),
        http.post('/api/auth/oidc/complete-login', () => {
          return HttpResponse.json({ returnUrl: '/profile/address' })
        })
      )

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/profile/address')
      })
    })
  })

  describe('token exchange failure', () => {
    it('shows a generic message when exchange-code endpoint fails (never raw IdP text)', async () => {
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ error: 'Token exchange failed' }, { status: 400 })
        })
      )

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Something went wrong.')).toBeInTheDocument()
      })
      expect(screen.queryByText('Token exchange failed')).not.toBeInTheDocument()
    })
  })

  describe('IdP error redirect (?error=)', () => {
    it('shows error message with short IdP description when safe', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=server_error&error_description=User+cancelled',
          href: 'http://localhost:3000/callback?error=server_error'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(
          screen.getByText('Primary MyColorado sign-in did not finish. User cancelled')
        ).toBeInTheDocument()
      })
    })

    it('shows error message without description when IdP omits it', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=server_error',
          href: 'http://localhost:3000/callback?error=server_error'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Primary MyColorado sign-in did not finish.')).toBeInTheDocument()
      })
    })

    it('does not render Ping/Socure connector blobs when description is structured JSON', async () => {
      const blob = JSON.stringify({
        code: 'errorResponse',
        interactionId: '03018f37-c15e-4da2-9f79-26dd163f9c9f',
        errors: { nested: { message: 'Error creating delayed response' } }
      })
      Object.defineProperty(window, 'location', {
        value: {
          search: `?error=invalid_request&error_description=${encodeURIComponent(blob)}`,
          href: 'http://localhost:3000/callback'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Primary MyColorado sign-in did not finish.')).toBeInTheDocument()
      })
      expect(screen.queryByText(/interactionId/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/errorResponse/i)).not.toBeInTheDocument()
    })

    it('shows the step-up declined screen when Socure consent text appears inside a connector blob', async () => {
      const blob = JSON.stringify({
        errors: {
          x: { additionalProperties: { errorMsg: 'User opted out' } }
        },
        additionalProperties: { errorObj: 'User denied consent' }
      })
      Object.defineProperty(window, 'location', {
        value: {
          search: `?error=invalid_request&error_description=${encodeURIComponent(blob)}`,
          href: 'http://localhost:3000/callback'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Identity verification was not completed')).toBeInTheDocument()
      })
      expect(
        screen.getByText(
          'You chose not to share information with our identity verification partner.'
        )
      ).toBeInTheDocument()
      expect(screen.queryByText(/interactionId/i)).not.toBeInTheDocument()
    })

    it('treats OAuth access_denied as step-up declined even when description is omitted', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied',
          href: 'http://localhost:3000/callback?error=access_denied'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Identity verification was not completed')).toBeInTheDocument()
      })
    })
  })

  describe('language toggle reactivity', () => {
    it('re-translates the error message when the user switches language after the error fires', async () => {
      Object.defineProperty(window, 'location', {
        value: { search: '', href: 'http://localhost:3000/callback' },
        writable: true
      })

      const { rerender } = render(<CallbackPage />)
      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })

      // Simulate the user toggling language: the bundle the mocked t() reads
      // from now returns Spanish copy. The component should re-render with
      // the new translation because we store the key, not the resolved string.
      const original = TEST_TRANSLATIONS.login!.callbackErrorMissingParams
      TEST_TRANSLATIONS.login!.callbackErrorMissingParams = 'Falta información de inicio de sesión.'
      try {
        rerender(<CallbackPage />)
        expect(screen.getByText('Falta información de inicio de sesión.')).toBeInTheDocument()
      } finally {
        TEST_TRANSLATIONS.login!.callbackErrorMissingParams = original!
      }
    })
  })

  describe('error redirect', () => {
    it('redirects to dashboard after showing a generic callback error', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })

      Object.defineProperty(window, 'location', {
        value: { search: '', href: 'http://localhost:3000/callback' },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })

      await vi.advanceTimersByTimeAsync(5000)

      expect(mockReplace).toHaveBeenCalledWith('/dashboard')

      vi.useRealTimers()
    })

    it('does not auto-redirect after step-up declined', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })

      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied',
          href: 'http://localhost:3000/callback?error=access_denied'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Identity verification was not completed')).toBeInTheDocument()
      })

      await vi.advanceTimersByTimeAsync(5000)

      expect(mockReplace).not.toHaveBeenCalled()

      vi.useRealTimers()
    })
  })
})
