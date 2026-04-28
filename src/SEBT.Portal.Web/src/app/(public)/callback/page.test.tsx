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
vi.mock('@/lib/translations', () => ({
  getTranslations: vi.fn().mockImplementation((namespace: string) => {
    const namespaces: Record<string, Record<string, string>> = {
      login: {
        callbackSigningIn: 'Signing you in…',
        callbackSignInIssue: 'Sign-in issue',
        callbackErrorMissingParams: 'Missing sign-in information.',
        callbackErrorGeneric: 'Something went wrong.',
        callbackErrorIdpRedirect: 'Primary MyColorado sign-in did not finish.'
      }
    }
    /* eslint-disable security/detect-object-injection -- test mock */
    const translations = namespaces[namespace] ?? {}
    return (key: string, defaultValue?: string) => translations[key] ?? defaultValue ?? key
    /* eslint-enable security/detect-object-injection */
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

    it('shows signing in message initially', () => {
      render(<CallbackPage />)
      expect(screen.getByText('Signing you in…')).toBeInTheDocument()
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
    it('shows error when exchange-code endpoint fails', async () => {
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ error: 'Token exchange failed' }, { status: 400 })
        })
      )

      render(<CallbackPage />)

      await waitFor(() => {
        expect(screen.getByText('Token exchange failed')).toBeInTheDocument()
      })
    })
  })

  describe('IdP error redirect (?error=)', () => {
    it('shows error message with IdP description', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied&error_description=User+cancelled',
          href: 'http://localhost:3000/callback?error=access_denied'
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
  })

  describe('error redirect', () => {
    it('redirects to login after showing error', async () => {
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

      expect(mockReplace).toHaveBeenCalledWith('/login')

      vi.useRealTimers()
    })
  })
})
