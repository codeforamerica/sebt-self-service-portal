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

vi.mock('@/lib/state', () => ({
  getState: vi.fn()
}))

vi.mock('@/lib/translations', () => ({
  getTranslations: vi.fn().mockImplementation((namespace: string) => {
    const namespaces: Record<string, Record<string, string>> = {
      login: {
        title: 'Log in to your account',
        body: 'Enter your email to receive a one-time code.',
        logInDisclaimerBody1: 'You can use the same login you use to access PEAK.',
        logInDisclaimerBody2: 'Contact us if you need assistance logging into your account.'
      }
    }
    const translations = namespaces[namespace] ?? {}
    return (key: string) => translations[key] ?? key
  })
}))

vi.mock('@/features/auth', () => ({
  LoginForm: () => <div data-testid="login-form">LoginForm</div>
}))

import { getState } from '@/lib/state'
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
          name: /Log in to your Summer EBT account/i
        })
      ).toBeInTheDocument()
    })

    it('applies text-primary-dark class to the title', () => {
      render(<LoginPage />)
      const heading = screen.getByRole('heading', {
        name: /Log in to your Summer EBT account/i
      })
      expect(heading).toHaveClass('text-primary-dark')
    })

    it('renders the PEAK body text', () => {
      render(<LoginPage />)
      expect(
        screen.getByText(/You can use the same login you use to access PEAK/i)
      ).toBeInTheDocument()
    })

    it('renders the Log in button with primary-dark styling', () => {
      render(<LoginPage />)
      const logInButton = screen.getByRole('link', { name: 'Log in' })
      expect(logInButton).toHaveClass('usa-button')
      expect(logInButton).toHaveClass('bg-primary-dark')
    })

    it('renders the Iniciar sesión outline button', () => {
      render(<LoginPage />)
      const espButton = screen.getByRole('link', { name: 'Iniciar sesión' })
      expect(espButton).toHaveAttribute('lang', 'es')
      expect(espButton).toHaveClass('usa-button--outline')
      expect(espButton).toHaveClass('border-primary')
    })

    it('renders the contact assistance link', () => {
      render(<LoginPage />)
      expect(
        screen.getByText('Contact us if you need assistance logging into your account.')
      ).toBeInTheDocument()
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
