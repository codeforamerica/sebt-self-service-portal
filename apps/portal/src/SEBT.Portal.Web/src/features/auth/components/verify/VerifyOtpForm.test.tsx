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

import { i18n } from '@sebt/design-system/client'

import amDcValidation from '@/content/locales/am/dc/validation.json'
import enDcValidation from '@/content/locales/en/dc/validation.json'
import esDcValidation from '@/content/locales/es/dc/validation.json'
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
    // RTL waitFor + default fake timers can exceed Vitest's 5s test timeout on slow CI;
    // these tests do not need timer mocking.
    beforeEach(() => {
      vi.useRealTimers()
    })

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

    it('names the fieldset group after the code label for assistive tech', async () => {
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        const group = screen.getByRole('group', { name: 'Enter the 6 digit confirmation code' })
        expect(group.querySelector('legend')).toHaveClass('usa-sr-only')
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

    it('should navigate to /login/id-proofing when ID proofing is InProgress', async () => {
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: true,
            email: TEST_EMAILS.success,
            ial: '1',
            idProofingStatus: 1,
            idProofingCompletedAt: null,
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
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      })
    })

    it('should navigate to /login/id-proofing when ID proofing is Failed', async () => {
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: true,
            email: TEST_EMAILS.success,
            ial: '1',
            idProofingStatus: 3,
            idProofingCompletedAt: null,
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
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      })
    })

    it('should navigate to /login/id-proofing when ID proofing is Expired', async () => {
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: true,
            email: TEST_EMAILS.success,
            ial: '1',
            idProofingStatus: 4,
            idProofingCompletedAt: null,
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
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      })
    })

    it('should show an error and not navigate when session refresh fails after valid OTP', async () => {
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: false,
            email: null,
            ial: null,
            idProofingStatus: null,
            idProofingCompletedAt: null,
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
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/error occurred on our end/i)).toBeInTheDocument()
        expect(mockPush).not.toHaveBeenCalled()
        expect(sessionStorage.getItem('otp_email')).toBe(TEST_EMAILS.success)
      })
    })

    it('should navigate to /login/id-proofing when idProofingStatus claim is absent', async () => {
      server.use(
        http.get('/api/auth/status', () =>
          HttpResponse.json({
            isAuthorized: true,
            email: TEST_EMAILS.success,
            ial: '1',
            idProofingCompletedAt: null,
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
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
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

      // The label stays plain "Confirm"; busy state is exposed via aria-busy
      // and the polite live region, not by mutating the accessible name
      expect(confirmButton).toHaveAttribute('aria-busy', 'true')
      expect(screen.queryByText(/confirm\.\.\./i)).not.toBeInTheDocument()
      expect(screen.getByText('Processing')).toHaveClass('usa-sr-only')
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

      // A 401 maps to the translatable, actionable otpInvalid copy ("enter a valid code,
      // tap to send a new code") rather than the raw English backend message (DC-454).
      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/enter a valid.*digit code/i)).toBeInTheDocument()
      })
    })

    it('should display error alert for invalid OTP when API returns 400', async () => {
      server.use(
        http.post('/api/auth/otp/validate', () =>
          HttpResponse.json({ error: 'Invalid OTP. Please try again.' }, { status: 400 })
        )
      )

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

      // The real validate endpoint returns 400 for a wrong code; treat it like 401.
      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/enter a valid.*digit code/i)).toBeInTheDocument()
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

      // Expired and invalid 401s share the same actionable "send a new code" copy; the
      // backend's English-only "expired" wording is no longer surfaced directly (DC-454).
      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByText(/enter a valid.*digit code/i)).toBeInTheDocument()
      })
    })
  })

  describe('Error language switching (DC-454)', () => {
    // Real timers: RTL waitFor + the i18n changeLanguage await can exceed the test timeout
    // under fake timers. The i18n instance is a shared singleton; reset to English after.
    beforeEach(() => {
      vi.useRealTimers()
    })

    afterEach(async () => {
      await act(async () => {
        await i18n.changeLanguage('en')
      })
    })

    it('re-translates the submit error across all DC languages, without resubmitting', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      const otpInput = await screen.findByRole('textbox', { name: /enter.*confirmation code/i })
      // A wrong 6-digit code reaches the server and returns 401 → the actionable otpInvalid copy.
      await user.type(otpInput, TEST_OTP.invalid)
      await user.click(screen.getByRole('button', { name: /confirm/i }))

      expect(await screen.findByText(enDcValidation.otpInvalid)).toBeInTheDocument()

      await act(async () => {
        await i18n.changeLanguage('es')
      })
      expect(await screen.findByText(esDcValidation.otpInvalid)).toBeInTheDocument()

      await act(async () => {
        await i18n.changeLanguage('am')
      })
      expect(await screen.findByText(amDcValidation.otpInvalid)).toBeInTheDocument()
      expect(screen.queryByText(enDcValidation.otpInvalid)).toBeNull()
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

    it('keeps the page processing state off while resend is in flight', async () => {
      // Resend's busy state is local to the Resend button (its own disabled +
      // countdown handling). The page-level treatment (fieldset fade, spinner,
      // "Processing" announcement) belongs to the Verify submit only.
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
      const { container } = renderWithProviders(
        <VerifyOtpForm
          email={TEST_EMAILS.success}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code/i })).toBeInTheDocument()
      })

      const otpInput = screen.getByRole('textbox', { name: /enter.*confirmation code/i })
      await user.click(screen.getByRole('button', { name: /resend code/i }))

      expect(otpInput).not.toBeDisabled()
      expect(container.querySelector('fieldset.usa-fieldset')).not.toHaveClass('opacity-50')
      expect(container.querySelector('.usa-spinner')).toBeNull()
      expect(screen.queryByText('Processing')).not.toBeInTheDocument()

      // Countdown behavior is unaffected
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resend code \(30s\)/i })).toBeInTheDocument()
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
    describe('form structure', () => {
      beforeEach(() => {
        vi.useRealTimers()
      })

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
