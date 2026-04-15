/**
 * VerifyOtpForm Component Unit Tests
 *
 * Tests the OTP verification form behavior including:
 * - Form rendering and accessibility
 * - OTP validation
 * - OTP submission
 * - Resend code functionality with cooldown
 * - Error handling for various scenarios
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { TEST_EMAILS, TEST_OTP } from '@/mocks/handlers'
import { server } from '@/mocks/server'

import { AuthProvider } from '../../context'
import { VerifyOtpForm } from './VerifyOtpForm'
import { VerifyOtpFormWrapper } from './VerifyOtpFormWrapper'

const TEST_CONTACT_LINK = 'https://example.com/contact'

// Mock next/navigation
const mockPush = vi.fn()
const mockReplace = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace
  })
}))

// Helper to create a fresh QueryClient for each test
function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

// Helper to render component with providers
function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createTestQueryClient()
  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{ui}</AuthProvider>
      </QueryClientProvider>
    ),
    queryClient
  }
}

describe('VerifyOtpForm', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockReplace.mockClear()
    sessionStorage.clear()
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('Rendering', () => {
    it('should render OTP input field', async () => {
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        expect(otpInput).toBeInTheDocument()
        expect(otpInput).toHaveAttribute('inputMode', 'numeric')
        expect(otpInput).toHaveAttribute('maxLength', '6')
      })
    })

    it('should render confirm button', async () => {
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        const confirmButton = screen.getByRole('button', { name: /confirm/i })
        expect(confirmButton).toBeInTheDocument()
        expect(confirmButton).toHaveAttribute('type', 'submit')
      })
    })

    it('should render resend code button', async () => {
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        const resendButton = screen.getByRole('button', { name: /resend code/i })
        expect(resendButton).toBeInTheDocument()
        expect(resendButton).toHaveAttribute('type', 'button')
      })
    })
  })

  describe('Form Submission', () => {
    it('should submit form with valid OTP and navigate on success', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      sessionStorage.setItem('otp_email', TEST_EMAILS.success)
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.valid)
      await user.click(confirmButton)

      await waitFor(() => {
        expect(sessionStorage.getItem('otp_email')).toBeNull()
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      })
    })

    it('should navigate to /dashboard when session reports ID proofing already complete', async () => {
      // Override /auth/status to return a completed id_proofing status
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: true,
            email: TEST_EMAILS.success,
            ial: '1plus',
            idProofingStatus: 2,
            idProofingCompletedAt: Math.floor(Date.now() / 1000),
            idProofingExpiresAt: null
          })
        )
      )

      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      sessionStorage.setItem('otp_email', TEST_EMAILS.success)
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.valid)
      await user.click(confirmButton)

      await waitFor(() => {
        expect(sessionStorage.getItem('otp_email')).toBeNull()
        expect(mockPush).toHaveBeenCalledWith('/dashboard')
      })
    })

    it('should show loading state during submission', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.valid)
      await user.click(confirmButton)

      // Button should show loading state
      expect(screen.getByRole('button', { name: /confirm\.\.\./i })).toBeInTheDocument()
    })

    it('should disable input during submission', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.valid)
      await user.click(confirmButton)

      expect(otpInput).toBeDisabled()
    })
  })

  describe('Validation', () => {
    it('should show error for empty OTP on blur', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })

      await user.click(otpInput)
      await user.tab()

      await waitFor(() => {
        const errorMessage = document.querySelector('.usa-error-message')
        expect(errorMessage).toBeInTheDocument()
        // i18n key: validation.required → "This is required"
        expect(errorMessage).toHaveTextContent(/this is required/i)
      })
    })

    it('should show error for invalid OTP length', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })

      await user.type(otpInput, '123')
      await user.tab()

      await waitFor(() => {
        // i18n key: validation.otpInvalid → "Enter a valid [6] digit code..."
        expect(screen.getByText(/enter a valid.*digit code/i)).toBeInTheDocument()
      })
    })
  })

  describe('Error Handling', () => {
    it('should display error alert for invalid OTP', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.invalid)
      await user.click(confirmButton)

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/invalid otp/i)).toBeInTheDocument()
      })
    })

    it('should display error alert for expired OTP', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.expired)
      await user.click(confirmButton)

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/expired/i)).toBeInTheDocument()
      })
    })
  })

  describe('Resend Code', () => {
    it('should resend code and show success message', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code/i })).toBeInTheDocument()
      })

      const resendButton = screen.getByRole('button', { name: /resend code/i })
      await user.click(resendButton)

      await waitFor(() => {
        expect(screen.getByText(/new code has been sent/i)).toBeInTheDocument()
      })
    })

    it('should show countdown after resending code', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code/i })).toBeInTheDocument()
      })

      const resendButton = screen.getByRole('button', { name: /resend code/i })
      await user.click(resendButton)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code \(30s\)/i })).toBeInTheDocument()
      })
    })

    it('should decrement countdown timer', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code/i })).toBeInTheDocument()
      })

      const resendButton = screen.getByRole('button', { name: /resend code/i })
      await user.click(resendButton)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code \(30s\)/i })).toBeInTheDocument()
      })

      // Advance timer by 5 seconds
      await act(async () => {
        vi.advanceTimersByTime(5000)
      })

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code \(25s\)/i })).toBeInTheDocument()
      })
    })

    it('should re-enable resend button after countdown completes', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code/i })).toBeInTheDocument()
      })

      const resendButton = screen.getByRole('button', { name: /resend code/i })
      await user.click(resendButton)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code \(30s\)/i })).toBeInTheDocument()
      })

      // Advance timer by 30 seconds
      await act(async () => {
        vi.advanceTimersByTime(30000)
      })

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^resend code$/i })).toBeInTheDocument()
        expect(screen.getByRole('button', { name: /^resend code$/i })).not.toBeDisabled()
      })
    })
  })

  describe('Accessibility', () => {
    it('should have accessible form structure', async () => {
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const form = document.querySelector('form')
      expect(form).toBeInTheDocument()

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      expect(otpInput).toHaveAttribute('aria-required', 'true')
    })

    it('should display error in alert role', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(
          screen.getByRole('textbox', { name: /enter.*confirmation code/i })
        ).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      const confirmButton = screen.getByRole('button', { name: /confirm/i })

      await user.type(otpInput, TEST_OTP.invalid)
      await user.click(confirmButton)

      await waitFor(() => {
        const alert = screen.getByRole('alert')
        expect(alert).toBeInTheDocument()
      })
    })
  })
})

describe('VerifyOtpFormWrapper', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockReplace.mockClear()
    sessionStorage.clear()
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('should redirect to login if no email in sessionStorage', () => {
    renderWithProviders(<VerifyOtpFormWrapper contactLink={TEST_CONTACT_LINK} />)

    expect(mockReplace).toHaveBeenCalledWith('/login')
  })

  it('should render form when email is in sessionStorage', async () => {
    sessionStorage.setItem('otp_email', TEST_EMAILS.success)
    renderWithProviders(<VerifyOtpFormWrapper contactLink={TEST_CONTACT_LINK} />)

    await waitFor(() => {
      expect(screen.getByRole('textbox', { name: /enter.*confirmation code/i })).toBeInTheDocument()
    })
  })
})
