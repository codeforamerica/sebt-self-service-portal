/**
 * OIDC Callback Page Unit Tests
 *
 * Tests the OIDC callback flow including:
 * - Successful token exchange and redirect to dashboard
 * - Missing code/state parameters → step-up failure page
 * - Exchange-code failure → step-up failure page
 * - IdP error redirect (?error=) → step-up failure page
 *
 * PKCE/sessionStorage validation tests have been removed — all flow metadata
 * (stateCode, isStepUp, returnUrl, state validation) is now handled server-side
 * via the pre-auth session (V04 fix).
 */
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { OIDC_CALLBACK_ERROR_OFF_BOARDING } from '@/features/auth/api/oidc'
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

const TEST_TRANSLATIONS: Record<string, Record<string, string>> = {
  login: {
    callbackSigningIn: 'Signing you in…'
  },
  'step-upProcessing': {
    title: 'Please wait...',
    body: 'Do not exit the page. Checking to see if we have enough information.'
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
    it('redirects to step-up failure when code is missing from URL', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?state=test-state',
          href: 'http://localhost:3000/callback?state=test-state'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })

    it('redirects to step-up failure when state is missing from URL', async () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?code=test-code', href: 'http://localhost:3000/callback?code=test-code' },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })
  })

  describe('successful flow', () => {
    beforeEach(() => {
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
    it('redirects to step-up failure without rendering raw IdP text', async () => {
      server.use(
        http.post('/api/auth/oidc/callback', () => {
          return HttpResponse.json({ error: 'Token exchange failed' }, { status: 400 })
        })
      )

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
      expect(screen.queryByText('Token exchange failed')).not.toBeInTheDocument()
    })
  })

  describe('IdP error redirect (?error=)', () => {
    it('redirects to step-up failure for server_error with description', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=server_error&error_description=User+cancelled',
          href: 'http://localhost:3000/callback?error=server_error'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })

    it('redirects to step-up failure when IdP omits error_description', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=server_error',
          href: 'http://localhost:3000/callback?error=server_error'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })

    it('redirects to step-up failure for structured JSON error_description (no blob rendered)', async () => {
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
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
      expect(screen.queryByText(/interactionId/i)).not.toBeInTheDocument()
    })

    it('redirects to step-up failure when Socure consent text appears inside a connector blob', async () => {
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
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })

    it('redirects to step-up failure for access_denied', async () => {
      Object.defineProperty(window, 'location', {
        value: {
          search: '?error=access_denied',
          href: 'http://localhost:3000/callback?error=access_denied'
        },
        writable: true
      })

      render(<CallbackPage />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(OIDC_CALLBACK_ERROR_OFF_BOARDING)
      })
    })
  })
})
