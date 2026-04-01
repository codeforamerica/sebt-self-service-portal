/**
 * OIDC Callback Page Unit Tests
 *
 * Tests the OIDC callback flow including:
 * - Successful token exchange and redirect to dashboard
 * - Missing code/state parameters
 * - PKCE session expired (no stored PKCE)
 * - PKCE state mismatch
 * - Exchange-code failure
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
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
      isAuthenticated: false,
      token: null
    }),
    setAuthToken: vi.fn()
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
        callbackErrorSessionExpired: 'Session expired.',
        callbackErrorStateMismatch: 'State mismatch.',
        callbackErrorGeneric: 'Something went wrong.',
        callbackErrorStepUpFailed: 'Step-up verification did not finish.',
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

// Mock PKCE storage
const mockGetPkce = vi.fn()
const mockClearPkce = vi.fn()
vi.mock('@/lib/oidc-pkce', () => ({
  getPkceFromStorage: (...args: unknown[]) => mockGetPkce(...args),
  clearPkceStorage: (...args: unknown[]) => mockClearPkce(...args)
}))

import CallbackPage from './page'

function renderCallbackPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <CallbackPage />
    </QueryClientProvider>
  )
}

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

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })
    })

    it('shows error when state is missing from URL', async () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?code=test-code', href: 'http://localhost:3000/callback?code=test-code' },
        writable: true
      })

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })
    })
  })

  describe('PKCE validation', () => {
    it('shows session expired when no PKCE data is stored', async () => {
      mockGetPkce.mockReturnValue(null)

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('Session expired.')).toBeInTheDocument()
      })
      expect(mockClearPkce).toHaveBeenCalled()
    })

    it('shows state mismatch when PKCE state does not match URL state', async () => {
      mockGetPkce.mockReturnValue({
        state: 'different-state-value',
        code_verifier: 'test-verifier',
        redirect_uri: 'http://localhost:3000/callback',
        token_endpoint: 'https://auth.example.com/token',
        client_id: 'test-client'
      })

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('State mismatch.')).toBeInTheDocument()
      })
      expect(mockClearPkce).toHaveBeenCalled()
    })
  })

  describe('successful flow', () => {
    beforeEach(() => {
      mockGetPkce.mockReturnValue({
        state: 'test-state-value',
        code_verifier: 'test-verifier',
        redirect_uri: 'http://localhost:3000/callback',
        token_endpoint: 'https://auth.example.com/token',
        client_id: 'test-client'
      })
      // getState returns 'co'; flow is callback (returns callbackToken) then complete-login (returns token)
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ callbackToken: 'mock-callback-token-for-testing' })
        }),
        http.post('/api/auth/oidc/complete-login', () => {
          return HttpResponse.json({ token: 'mock-jwt-token-for-testing' })
        })
      )
    })

    it('shows signing in message initially', () => {
      renderCallbackPage()
      expect(screen.getByText('Signing you in…')).toBeInTheDocument()
    })

    it('redirects to dashboard on successful login', async () => {
      renderCallbackPage()

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/dashboard')
      })
      expect(mockLogin).toHaveBeenCalledWith('mock-jwt-token-for-testing')
    })
  })

  describe('token exchange failure', () => {
    beforeEach(() => {
      mockGetPkce.mockReturnValue({
        state: 'test-state-value',
        code_verifier: 'test-verifier',
        redirect_uri: 'http://localhost:3000/callback',
        token_endpoint: 'https://auth.example.com/token',
        client_id: 'test-client'
      })
    })

    it('shows error when exchange-code endpoint fails', async () => {
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ error: 'Token exchange failed' }, { status: 400 })
        })
      )

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('Token exchange failed')).toBeInTheDocument()
      })
    })
  })

  describe('IdP error redirect (?error=)', () => {
    it('shows step-up message when PKCE marks isStepUp', async () => {
      mockGetPkce.mockReturnValue({ isStepUp: true })
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied&error_description=User+cancelled',
          href: 'http://localhost:3000/callback?error=access_denied'
        },
        writable: true
      })

      renderCallbackPage()

      await waitFor(() => {
        expect(
          screen.getByText('Step-up verification did not finish. User cancelled')
        ).toBeInTheDocument()
      })
    })

    it('shows primary sign-in message when not step-up', async () => {
      mockGetPkce.mockReturnValue({ isStepUp: false })
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied&error_description=User+cancelled',
          href: 'http://localhost:3000/callback?error=access_denied'
        },
        writable: true
      })

      renderCallbackPage()

      await waitFor(() => {
        expect(
          screen.getByText('Primary MyColorado sign-in did not finish. User cancelled')
        ).toBeInTheDocument()
      })
    })

    it('treats missing PKCE as primary sign-in for IdP error copy', async () => {
      mockGetPkce.mockReturnValue(null)
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=server_error',
          href: 'http://localhost:3000/callback?error=server_error'
        },
        writable: true
      })

      renderCallbackPage()

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

      renderCallbackPage()

      await waitFor(() => {
        expect(screen.getByText('Missing sign-in information.')).toBeInTheDocument()
      })

      await vi.advanceTimersByTimeAsync(5000)

      expect(mockReplace).toHaveBeenCalledWith('/login')

      vi.useRealTimers()
    })
  })
})
