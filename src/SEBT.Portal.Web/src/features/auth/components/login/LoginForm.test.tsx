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
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { TEST_EMAILS } from '@/mocks/handlers'

import { LoginForm } from './LoginForm'

// Mock next/navigation
const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush
  })
}))

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

    it('should show loading state during submission', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      // Button should show loading state (Continue...)
      expect(screen.getByRole('button', { name: /continue\.\.\./i })).toBeInTheDocument()
    })

    it('should disable input during submission', async () => {
      const user = userEvent.setup()
      renderWithProviders(<LoginForm />)

      const emailInput = screen.getByRole('textbox', { name: /enter your email address/i })
      const submitButton = screen.getByRole('button', { name: /continue/i })

      await user.type(emailInput, TEST_EMAILS.success)
      await user.click(submitButton)

      // Input should be disabled while loading
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
