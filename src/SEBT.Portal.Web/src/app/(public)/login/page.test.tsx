/**
 * Login Page Unit Tests (Co-located)
 *
 * Tests the login page for both CO and DC states.
 * CO renders the external auth landing page (COLoginPage).
 * DC renders the OTP email form (LoginForm).
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import LoginPage from './page'

vi.mock('@/lib/state', () => ({
  getState: vi.fn()
}))

vi.mock('@/lib/translations', () => ({
  getTranslations: vi.fn().mockImplementation((namespace: string) => {
    const namespaces: Record<string, Record<string, string>> = {
      login: {
        title: 'Access your Summer EBT account',
        body: 'Enter your email to receive a one-time code.',
        logInDisclaimerBody1:
          'After tapping "Log in" you\'ll be redirected to log in using your myColorado™ account.',
        logInDisclaimerBody2: 'Contact us if you need assistance logging into your account.',
        oidcErrorConfigLoad: 'Unable to load login configuration.'
      },
      common: {
        logIn: 'Log in with myColorado™',
        logInEsp: 'Iniciar sesión con myColorado™'
      }
    }
    /* eslint-disable security/detect-object-injection -- test mock; namespace and key are controlled */
    const translations = namespaces[namespace] ?? {}
    return (key: string, defaultValue?: string) => translations[key] ?? defaultValue ?? key
    /* eslint-enable security/detect-object-injection */
  })
}))

vi.mock('@/features/auth', () => ({
  LoginForm: () => <div data-testid="login-form">LoginForm</div>,
  OidcConfigResponseSchema: {},
  OidcCallbackTokenResponseSchema: {},
  OidcCompleteLoginResponseSchema: {}
}))

vi.mock('@/api', () => ({
  apiFetch: vi.fn()
}))

import { getState } from '@/lib/state'
const mockGetState = vi.mocked(getState)

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('CO state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('co')
    })

    it('renders the login title', () => {
      renderWithQueryClient(<LoginPage />)
      expect(
        screen.getByRole('heading', {
          name: /Access your Summer EBT account/i
        })
      ).toBeInTheDocument()
    })

    it('applies text-primary-dark class to the title', () => {
      renderWithQueryClient(<LoginPage />)
      const heading = screen.getByRole('heading', {
        name: /Access your Summer EBT account/i
      })
      expect(heading).toHaveClass('text-primary-dark')
    })

    it('renders the disclaimer body text', () => {
      renderWithQueryClient(<LoginPage />)
      expect(
        screen.getByText(/you'll be redirected to log in using your myColorado/i)
      ).toBeInTheDocument()
    })

    it('renders the Log in button with primary-dark styling', () => {
      renderWithQueryClient(<LoginPage />)
      const logInButton = screen.getByRole('button', { name: /Log in with myColorado/i })
      expect(logInButton).toHaveClass('usa-button')
      expect(logInButton).toHaveClass('bg-primary-dark')
    })

    it('renders the Iniciar sesión outline button', () => {
      renderWithQueryClient(<LoginPage />)
      const espButton = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })
      expect(espButton).toHaveAttribute('lang', 'es')
      expect(espButton).toHaveClass('usa-button--outline')
      expect(espButton).toHaveClass('border-primary')
    })

    it('renders the contact assistance link', () => {
      renderWithQueryClient(<LoginPage />)
      expect(
        screen.getByText('Contact us if you need assistance logging into your account.')
      ).toBeInTheDocument()
    })

    it('does not render LoginForm', () => {
      renderWithQueryClient(<LoginPage />)
      expect(screen.queryByTestId('login-form')).not.toBeInTheDocument()
    })
  })

  describe('DC state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })

    it('renders LoginForm', () => {
      render(<LoginPage />)
      expect(screen.getByTestId('login-form')).toBeInTheDocument()
    })

    it('does not apply text-primary-dark to the title', () => {
      render(<LoginPage />)
      const heading = screen.getByRole('heading', { level: 1 })
      expect(heading).not.toHaveClass('text-primary-dark')
    })
  })
})
