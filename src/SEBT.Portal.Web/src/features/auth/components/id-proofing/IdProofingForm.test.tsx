/**
 * IdProofingForm Component Unit Tests
 *
 * Tests the identity-proofing form behavior including:
 * - Rendering of ID options
 * - Conditional text input visibility based on radio selection
 * - DOB field validation
 * - ID value validation
 * - Successful submission and redirect
 * - API error display
 *
 * Radio button labels are queried by their translated text (DC locale, en).
 * Missing i18n keys (e.g. optionLabelNone) fall back to the key string itself.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import {
  SK_CHALLENGE_ID,
  SK_STILL_CHECKING,
  SK_SUB_STATE
} from '@/features/auth/components/doc-verify/sessionKeys'
import { AuthProvider } from '../../context'
import { IdProofingForm, type IdOption } from './IdProofingForm'

const TEST_CONTACT_LINK = 'https://example.com/contact'

// Mock next/navigation
const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn()
  })
}))

// Options used across tests. Labels/inputLabels match DC en translations.
// Keys that are not yet in the locale JSON fall back to the key string itself.
const TEST_ID_OPTIONS: IdOption[] = [
  { value: 'ssn', labelKey: 'optionLabelSsn', inputLabelKey: 'labelSsn' },
  { value: 'itin', labelKey: 'optionLabelItin', inputLabelKey: 'labelItin' },
  // optionLabelNone is not yet in the locale JSON, so t() returns the key string
  { value: 'none', labelKey: 'optionLabelNone' }
]

// Resolved translated strings / patterns for querying.
// Radio labels are exact translated strings (no required asterisk appended).
// InputField labels include an appended " *" span when isRequired is true,
// so regex patterns are used to match them.
const LABEL_SSN = 'Social Security Number (SSN)'
const LABEL_ITIN = 'Individual Taxpayer ID Number (ITIN)'
const LABEL_NONE = 'optionLabelNone' // missing key → falls back to key
const INPUT_LABEL_SSN = /Enter your Social Security Number/i
const INPUT_LABEL_ITIN = /Enter your Individual Taxpayer ID Number/i
// Day/Year InputFields also append " *", so use partial-match patterns
const INPUT_LABEL_DAY = /Day/i
const INPUT_LABEL_YEAR = /Year/i

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

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

describe('IdProofingForm', () => {
  beforeEach(() => {
    mockPush.mockClear()
  })

  describe('Rendering', () => {
    it('renders all provided ID options as radio buttons', () => {
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
    })

    it('renders Continue button', () => {
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
    })

    it('does not show an ID value text input before any radio is selected', () => {
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      expect(screen.queryByRole('textbox', { name: INPUT_LABEL_SSN })).not.toBeInTheDocument()
      expect(screen.queryByRole('textbox', { name: INPUT_LABEL_ITIN })).not.toBeInTheDocument()
    })
  })

  describe('Radio selection behaviour', () => {
    it('shows the corresponding text input when an ID-bearing option is selected', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))

      expect(await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })).toBeInTheDocument()
    })

    it('hides the text input when "none of the above" is selected', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // First select SSN so an input appears, then switch to "none"
      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      expect(await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })).toBeInTheDocument()

      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await waitFor(() => {
        expect(screen.queryByRole('textbox', { name: INPUT_LABEL_SSN })).not.toBeInTheDocument()
      })
    })

    it('clears the ID value when switching between options', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      const ssnInput = await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })
      await user.type(ssnInput, '999999999')

      await user.click(screen.getByRole('radio', { name: LABEL_ITIN }))
      const itinInput = await screen.findByRole('textbox', { name: INPUT_LABEL_ITIN })
      expect(itinInput).toHaveValue('')
    })
  })

  describe('DOB validation', () => {
    it('shows errors for all three DOB fields when all are empty on submit', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        // month error is a <span role="alert">, day/year errors are inside InputField's role="alert"
        // id type error is also a <span role="alert"> since no radio is selected
        const errors = screen.getAllByRole('alert')
        expect(errors).toHaveLength(4)
      })
    })

    it('shows only day and year errors when month is filled', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        // day/year errors plus id type error (no radio selected)
        const errors = screen.getAllByRole('alert')
        expect(errors).toHaveLength(3)
      })
    })
  })

  describe('ID value validation', () => {
    it('shows an error when an ID type is selected but no value entered', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // Fill all DOB fields
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '01')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')

      // Select SSN but leave the value blank
      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
    })
  })

  describe('ID type validation', () => {
    it('shows an error when the user submits without selecting an ID option', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // Fill valid DOB so only the radio error fires
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '01')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')

      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(mockPush).not.toHaveBeenCalled()
    })
  })

  describe('Successful submission', () => {
    it('redirects to /dashboard when identity is matched', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '03')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '10')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      await user.type(await screen.findByRole('textbox', { name: INPUT_LABEL_SSN }), '999999999')

      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/dashboard')
      })
    })
  })

  describe('Response routing', () => {
    it('navigates to doc-verify and stores challengeId when documentVerificationRequired', async () => {
      server.use(
        http.post('/api/id-proofing', () => {
          return HttpResponse.json({
            result: 'documentVerificationRequired',
            challengeId: 'challenge-abc',
            allowIdRetry: true
          })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          '/login/id-proofing/doc-verify?challengeId=challenge-abc'
        )
      })
      expect(sessionStorage.getItem(SK_CHALLENGE_ID)).toBe('challenge-abc')
    })

    it('clears stale DocV session keys before navigating to doc-verify', async () => {
      // Simulate leftover state from a prior DocV attempt
      sessionStorage.setItem(SK_CHALLENGE_ID, 'old-challenge-id')
      sessionStorage.setItem(SK_SUB_STATE, 'pending')
      sessionStorage.setItem(SK_STILL_CHECKING, 'true')

      server.use(
        http.post('/api/id-proofing', () => {
          return HttpResponse.json({
            result: 'documentVerificationRequired',
            challengeId: 'new-challenge-id',
            allowIdRetry: true
          })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          '/login/id-proofing/doc-verify?challengeId=new-challenge-id'
        )
      })

      // Old sub-state and timer keys should have been cleared before navigation
      expect(sessionStorage.getItem(SK_SUB_STATE)).toBeNull()
      expect(sessionStorage.getItem(SK_STILL_CHECKING)).toBeNull()
      // The new challengeId should be set (by the existing sessionStorage.setItem call)
      expect(sessionStorage.getItem(SK_CHALLENGE_ID)).toBe('new-challenge-id')
    })

    it('shows error when documentVerificationRequired but challengeId is missing', async () => {
      server.use(
        http.post('/api/id-proofing', () => {
          return HttpResponse.json({
            result: 'documentVerificationRequired'
            // challengeId deliberately omitted
          })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(screen.getByText(/unable to start document verification/i)).toBeInTheDocument()
      })
      expect(mockPush).not.toHaveBeenCalled()
    })

    it('redirects to off-boarding page when identity proofing fails', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')

      // "None of the above" triggers a 'failed' result in the mock
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding')
      })
    })

    it('passes canApply=false query param when API indicates no apply option', async () => {
      server.use(
        http.post('/api/id-proofing', () => {
          return HttpResponse.json({ result: 'failed', canApply: false })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')

      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding?canApply=false')
      })
    })
  })

  describe('API error handling', () => {
    it('shows a submit error alert when the API returns an error', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // Override the MSW handler to simulate an API error.
      // Using 400 (not 500) because the mutation retries 5xx errors with exponential backoff,
      // which would cause the test to time out. The mutation's retry logic short-circuits on 4xx.
      server.use(
        http.post('/api/id-proofing', () => {
          return HttpResponse.json({ error: 'Test API error' }, { status: 400 })
        })
      )

      // Fill valid DOB
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '01')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')

      // Select "none" — no ID value required
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong')
      })
      expect(mockPush).not.toHaveBeenCalled()
    })
  })
})
