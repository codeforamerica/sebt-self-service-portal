/**
 * LoginForm Component Unit Tests
 *
 * Tests the login form behavior including:
 * - Form rendering and accessibility
 * - Email validation
 * - OTP request submission
 * - Error handling for various scenarios
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { i18n } from '@sebt/design-system/client'

import amDcValidation from '@/content/locales/am/dc/validation.json'
import enDcValidation from '@/content/locales/en/dc/validation.json'
import esDcValidation from '@/content/locales/es/dc/validation.json'
import { TEST_EMAILS } from '@/mocks/handlers'

import { LoginForm } from './LoginForm'

// Mock next/navigation
const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush
  })
}))

// Mock analytics to spy on trackEvent without needing a live data layer.
const mockTrackEvent = vi.fn()
vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      trackEvent: mockTrackEvent,
      pageLoad: vi.fn(),
      setPageData: vi.fn(),
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserData: vi.fn(),
      setUserProfile: vi.fn(),
      get: vi.fn()
    })
  }
})

// Helper to create a fresh QueryClient for each test
// Important: We disable retries to avoid waiting for exponential backoff in tests
function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

// Override the mutation's built-in retry to ensure it's disabled in tests
// The useRequestOtp hook has its own retry logic that ignores QueryClient defaults

// Helper to render component with providers
function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createTestQueryClient()
  return {
    ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>),
    queryClient
  }
}

describe('LoginForm', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockTrackEvent.mockClear()
    sessionStorage.clear()
  })

  describe('Rendering', () => {
    it('should render email input field', () => {
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      expect(emailInput).toBeInTheDocument()
      expect(emailInput).toHaveAttribute('type', 'email')
    })

    it('should render submit button', () => {
      renderWithProviders(<LoginForm />)

      const submitButton = screen.getByRole('button', { name: /continue/i })
      expect(submitButton).toBeInTheDocument()
      expect(submitButton).toHaveAttribute('type', 'submit')
    })

    it('exposes data-analytics-cta on the submit button for cta_click tracking', () => {
      renderWithProviders(<LoginForm />)

      const submitButton = screen.getByRole('button', { name: /continue/i })
      expect(submitButton).toHaveAttribute('data-analytics-cta', 'login_cta')
    })

    it('names the fieldset group after the email label for assistive tech', () => {
      renderWithProviders(<LoginForm />)

      const group = screen.getByRole('group', { name: 'Enter your email address' })
      expect(group.querySelector('legend')).toHaveClass('usa-sr-only')
    })
  })

  describe('Form Submission', () => {
    it('should submit form with valid email, store email in sessionStorage, and navigate on success', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      await waitFor(() => {
        expect(sessionStorage.getItem('otp_email')).toBe(TEST_EMAILS.success)
        expect(mockPush).toHaveBeenCalledWith('/login/verify')
      })
    })

    it('should show the processing state during submission', async () => {
      const user = userEvent.setup()
      const { container } = renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: 'Continue' })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      // The label stays plain "Continue"; busy state is exposed via aria-busy
      // and the polite live region, not by mutating the accessible name
      expect(submitButton).toHaveAttribute('aria-busy', 'true')
      expect(submitButton).toBeDisabled()
      expect(screen.queryByText('Continue...')).not.toBeInTheDocument()
      expect(screen.getByText('Processing')).toHaveClass('usa-sr-only')
      expect(container.querySelector('fieldset.usa-fieldset')).toHaveClass('opacity-50')
    })

    it('should disable input during submission', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      // Input should be disabled while loading (via the surrounding fieldset)
      expect(emailInput).toBeDisabled()
    })
  })

  describe('Error Handling', () => {
    it('should display error alert for API errors', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.rateLimit)
      await user.click(submitButton)

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
      })
    })

    it('should recover from error and navigate on successful retry', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      // Trigger an error
      await user.type(emailInput, TEST_EMAILS.rateLimit)
      await user.click(submitButton)

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
      })

      // Clear input and type new value - should succeed
      await user.clear(emailInput)
      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalled()
      })
    })
  })

  describe('Analytics', () => {
    it('fires otp_request when a valid email is submitted', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      await waitFor(() => {
        expect(mockTrackEvent).toHaveBeenCalledWith('otp_request')
      })
    })
  })

  describe('Error language switching (DC-454)', () => {
    // The i18n instance is a shared singleton; reset to English so sibling tests
    // (which assume English labels) aren't affected by a lingering Spanish switch.
    afterEach(async () => {
      await act(async () => {
        await i18n.changeLanguage('en')
      })
    })

    it('re-translates the submit error across all DC languages, without resubmitting', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      // 429 (rate limit) is a client error: the request hook does not retry it, so the error
      // surfaces deterministically. Any API failure maps to the translatable globalInternalError
      // copy (not the raw English backend text). Assert against the locale JSON for each DC language.
      await user.type(emailInput, TEST_EMAILS.rateLimit)
      await user.click(submitButton)

      expect(await screen.findByText(enDcValidation.globalInternalError)).toBeInTheDocument()

      // Cycle DC languages with the error on screen — no resubmit.
      await act(async () => {
        await i18n.changeLanguage('es')
      })
      expect(await screen.findByText(esDcValidation.globalInternalError)).toBeInTheDocument()

      await act(async () => {
        await i18n.changeLanguage('am')
      })
      expect(await screen.findByText(amDcValidation.globalInternalError)).toBeInTheDocument()
      expect(screen.queryByText(enDcValidation.globalInternalError)).toBeNull()
    })

    it('re-translates the email validation error across all DC languages', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, 'not-an-email')
      await user.click(submitButton)

      expect(await screen.findByText(enDcValidation.enterEmail)).toBeInTheDocument()

      await act(async () => {
        await i18n.changeLanguage('es')
      })
      expect(await screen.findByText(esDcValidation.enterEmail)).toBeInTheDocument()

      await act(async () => {
        await i18n.changeLanguage('am')
      })
      expect(await screen.findByText(amDcValidation.enterEmail)).toBeInTheDocument()
      expect(screen.queryByText(enDcValidation.enterEmail)).toBeNull()
    })
  })

  describe('Accessibility', () => {
    it('should have accessible form structure', () => {
      renderWithProviders(<LoginForm />)

      // Form should be present
      const form = document.querySelector('form')
      expect(form).toBeInTheDocument()

      // Email input should have proper label association
      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      expect(emailInput).toBeInTheDocument()
      expect(emailInput).toHaveAttribute('aria-required', 'true')
    })

    it('should display error in alert role', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.rateLimit)
      await user.click(submitButton)

      await waitFor(() => {
        const alert = screen.getByRole('alert')
        expect(alert).toBeInTheDocument()
      })
    })
  })
})
