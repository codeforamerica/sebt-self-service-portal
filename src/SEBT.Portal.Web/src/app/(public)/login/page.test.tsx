/**
 * Login Page Unit Tests (Co-located)
 *
 * Tests the login page for both CO and DC states.
 * CO renders the external auth landing page (COLoginPage).
 * DC renders the OTP email form (LoginForm).
 */
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import LoginPage from './page'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() })
}))

vi.mock('@sebt/design-system', () => ({
  getState: vi.fn(),
  getStateLinks: vi.fn().mockReturnValue({ external: { contactUsAssistance: '' } }),
  TextLink: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  )
}))

// COLoginPage now uses the client-side useTranslation() hook (DC-187 fix).
// Mock the hook with CO-specific copy so the CO-state tests stay isolated from
// the project's real i18n init (which boots in DC mode for tests).
const TEST_TRANSLATIONS: Record<string, Record<string, string>> = {
  login: {
    title: 'Access your Summer EBT account',
    body: 'Enter your email to receive a one-time code.',
    logInDisclaimerBody1:
      'After tapping "Log in" you\'ll be redirected to log in using your myColorado™ account.',
    logInDisclaimerBody2: 'Contact us if you need assistance logging into your account.'
  },
  common: {
    logIn: 'Log in with myColorado™',
    logInEsp: 'Iniciar sesión con myColorado™'
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

vi.mock('@/features/auth', () => ({
  LoginForm: () => <div data-testid="login-form">LoginForm</div>,
  useAuth: () => ({ isAuthenticated: false })
}))

import { getState } from '@sebt/design-system'
const mockGetState = vi.mocked(getState)

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('CO state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('co')
    })

    it('renders the login title', () => {
      render(<LoginPage />)
      expect(
        screen.getByRole('heading', {
          name: /Access your Summer EBT account/i
        })
      ).toBeInTheDocument()
    })

    it('applies text-primary-dark class to the title', () => {
      render(<LoginPage />)
      const heading = screen.getByRole('heading', {
        name: /Access your Summer EBT account/i
      })
      expect(heading).toHaveClass('text-primary-dark')
    })

    it('renders the disclaimer body text', () => {
      render(<LoginPage />)
      expect(
        screen.getByText(/you'll be redirected to log in using your myColorado/i)
      ).toBeInTheDocument()
    })

    it('renders the Log in button with myColorado branded styling', () => {
      render(<LoginPage />)
      const logInButton = screen.getByRole('button', { name: /Log in with myColorado/i })
      expect(logInButton).toHaveClass('usa-button')
      expect(logInButton).toHaveClass('usa-button--mycolorado')
      expect(logInButton).not.toHaveClass('usa-button--outline')
    })

    it('renders the Iniciar sesión button as an outlined myColorado variant', () => {
      render(<LoginPage />)
      const espButton = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })
      expect(espButton).toHaveAttribute('lang', 'es')
      expect(espButton).toHaveClass('usa-button--mycolorado')
      expect(espButton).toHaveClass('usa-button--outline')
    })

    it('renders the myColorado logo inside both auth buttons', () => {
      render(<LoginPage />)
      const logInButton = screen.getByRole('button', { name: /Log in with myColorado/i })
      const espButton = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })
      expect(logInButton.querySelector('[data-testid="mycolorado-logo"]')).toBeInTheDocument()
      expect(espButton.querySelector('[data-testid="mycolorado-logo"]')).toBeInTheDocument()
    })

    it('does not render LoginForm', () => {
      render(<LoginPage />)
      expect(screen.queryByTestId('login-form')).not.toBeInTheDocument()
    })
  })

  describe('DC state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })

    it('renders the contact assistance link', () => {
      render(<LoginPage />)
      expect(
        screen.getByText('Contact us if you need assistance logging into your account.')
      ).toBeInTheDocument()
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
