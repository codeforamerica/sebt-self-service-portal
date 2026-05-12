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

// Mock analytics to spy on setPageData / trackEvent without needing a data layer.
const mockSetPageData = vi.fn()
const mockSetUserData = vi.fn()
const mockTrackEvent = vi.fn()
vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      setPageData: mockSetPageData,
      setUserData: mockSetUserData,
      trackEvent: mockTrackEvent,
      pageLoad: vi.fn(),
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserProfile: vi.fn(),
      get: vi.fn()
    })
  }
})

// Options used across tests. Labels/inputLabels match DC en translations.
// The "none" option uses a cross-namespace lookup (common:noneOfTheAbove) because
// the label key only lives in the common namespace.
// Per-option `validation` mirrors DC_ID_OPTIONS in page.tsx so shape-enforcement
// tests exercise the same path users hit in production.
const TEST_ID_OPTIONS: IdOption[] = [
  {
    value: 'ssn',
    labelKey: 'optionLabelSsn',
    inputLabelKey: 'labelSsn',
    validation: { digits: 9 }
  },
  {
    value: 'itin',
    labelKey: 'optionLabelItin',
    inputLabelKey: 'labelItin',
    validation: { digits: 9 }
  },
  {
    value: 'medicaidId',
    labelKey: 'optionLabelMedicaidId',
    inputLabelKey: 'labelMedicaidId',
    validation: { digits: [7, 8] }
  },
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    inputLabelKey: 'labelAccountId',
    validation: { digits: [7, 8] }
  },
  {
    value: 'snapPersonId',
    labelKey: 'optionPersonId',
    inputLabelKey: 'labelPersonId',
    validation: { digits: [7, 8] }
  },
  { value: 'none', labelKey: 'common:noneOfTheAbove' }
]

// Resolved translated strings / patterns for querying.
// Radio labels are exact translated strings (no required asterisk appended).
// InputField labels include an appended " *" span when isRequired is true,
// so regex patterns are used to match them.
const LABEL_SSN = 'Social Security Number (SSN)'
const LABEL_ITIN = 'Individual Taxpayer ID Number (ITIN)'
const LABEL_NONE = 'None of the above'
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
    mockSetPageData.mockClear()
    mockSetUserData.mockClear()
    mockTrackEvent.mockClear()
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

    it('resolves cross-namespace labelKey (common:noneOfTheAbove) to the translated text', () => {
      // Regression guard: the "none" option's label lives in the common namespace,
      // while the form calls useTranslation('idProofing'). Without the "common:"
      // prefix, i18next falls back to the raw key ("optionLabelNone" or similar).
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      const noneRadio = screen.getByRole('radio', { name: 'None of the above' })
      expect(noneRadio).toBeInTheDocument()
      // Should not leak the raw i18n key into the UI.
      expect(screen.queryByText(/^optionLabelNone$/)).not.toBeInTheDocument()
      expect(screen.queryByText(/^noneOfTheAbove$/)).not.toBeInTheDocument()
    })

    it('falls back to the raw key when a label key is missing from the active namespace (demonstrates the bug)', () => {
      // Baseline demonstration: when labelKey is NOT prefixed with common: and
      // the key does not exist in the idProofing namespace, i18next returns
      // the key string itself. This is the behaviour that surfaced the
      // literal "optionLabelNone" label in the UI before the fix.
      const BROKEN_OPTIONS: IdOption[] = [
        { value: 'ssn', labelKey: 'optionLabelSsn', inputLabelKey: 'labelSsn' },
        { value: 'none', labelKey: 'optionLabelNone' }
      ]

      renderWithProviders(
        <IdProofingForm
          idOptions={BROKEN_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // With the broken key, the label text is literally the key string.
      expect(screen.getByRole('radio', { name: 'optionLabelNone' })).toBeInTheDocument()
      expect(screen.queryByRole('radio', { name: 'None of the above' })).not.toBeInTheDocument()
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

      // "None of the above" triggers a 'failed' result in the mock,
      // which also returns offboardingReason so the URL carries a reason param.
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding?reason=noIdProvided')
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

    it('appends offboardingReason as a URL query param so the off-boarding route can branch copy', async () => {
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
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding?reason=noIdProvided')
      })
    })
  })

  describe('Shape validation (DC-296)', () => {
    it('strips non-digit characters from SSN input as the user types', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      const ssnInput = await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })
      await user.type(ssnInput, '555-44-3333')

      // User typed hyphens; state should only hold the 9 digits.
      expect(ssnInput).toHaveValue('555443333')
    })

    it('sets inputMode="numeric" and maxLength=9 on the SSN input', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      const ssnInput = await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })
      expect(ssnInput).toHaveAttribute('inputMode', 'numeric')
      expect(ssnInput).toHaveAttribute('maxLength', '9')
    })

    it('sets inputMode="numeric" and maxLength=9 on the ITIN input', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.click(screen.getByRole('radio', { name: LABEL_ITIN }))
      const itinInput = await screen.findByRole('textbox', { name: INPUT_LABEL_ITIN })
      expect(itinInput).toHaveAttribute('inputMode', 'numeric')
      expect(itinInput).toHaveAttribute('maxLength', '9')
    })

    it('blocks submit and shows a field-level error when SSN is too short', async () => {
      // With the digit-stripping onChange + maxLength=9, users literally cannot
      // type a 10-digit value. An 8-digit value is the realistic "wrong shape"
      // case — verify the form blocks it and does not call the mutation.
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '01')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')

      await user.click(screen.getByRole('radio', { name: LABEL_SSN }))
      const ssnInput = await screen.findByRole('textbox', { name: INPUT_LABEL_SSN })
      await user.type(ssnInput, '12345678')

      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(submitCalled).toBe(false)
      expect(mockPush).not.toHaveBeenCalled()
    })

    it('blocks submit and shows a DOB error when the DOB is in the future', async () => {
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      const futureYear = String(new Date().getFullYear() + 5)
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), futureYear)

      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(submitCalled).toBe(false)
      expect(mockPush).not.toHaveBeenCalled()
    })
  })

  describe('Composite DOB error routing (DC-296 follow-up)', () => {
    // When the schema rejects the DOB as a whole (impossible calendar date,
    // future, >120 years ago) the message describes a property of the *fieldset*,
    // not of any single input. Surface it at the fieldset level rather than
    // arbitrarily attaching it to the year input.
    it('routes a calendar-invalid DOB (Feb 31) to fieldset level, not the year input', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // Feb 31, 1990: month and year are individually fine; the combination is impossible.
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '02')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '31')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      const errorMessage = await screen.findByText(/valid date of birth/i)

      // Year value is fine — it should not be flagged invalid.
      const yearInput = screen.getByRole('textbox', { name: INPUT_LABEL_YEAR })
      expect(yearInput).not.toHaveAttribute('aria-invalid', 'true')

      // The message belongs to the date-of-birth fieldset, not to any single
      // input's usa-form-group wrapper.
      expect(errorMessage.closest('.usa-form-group')).toBeNull()

      expect(mockPush).not.toHaveBeenCalled()
    })
  })

  // TODO: Re-enable once `optionLabelMedicaidId`, `labelMedicaidId`, `optionPersonId`,
  // and `labelPersonId` are present in the DC idProofing locale (CSV currently has
  // !N/A! for these — DC's id-proofing flow doesn't surface Medicaid/SNAP-person
  // options today). Without those keys the radio buttons render with no accessible
  // name and the queries below can't find them. Either restore the CSV rows or
  // restructure these tests to mock the i18n bundle for the missing keys.
  describe.skip('Per-option digit validation (DC-296)', () => {
    // medicaidId: 7 or 8 digits accepted (per DC CSV).
    const LABEL_MEDICAID = 'Medicaid ID'
    const INPUT_LABEL_MEDICAID = /Enter your Medicaid ID/i

    async function fillDobAndSelect(idLabel: string) {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )
      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '01')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '15')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1990')
      await user.click(screen.getByRole('radio', { name: idLabel }))
      return user
    }

    it('rejects a 6-digit Medicaid ID (below range)', async () => {
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = await fillDobAndSelect(LABEL_MEDICAID)
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      await user.type(input, '123456')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(submitCalled).toBe(false)
      expect(mockPush).not.toHaveBeenCalled()
    })

    it('accepts a 7-digit Medicaid ID (lower bound)', async () => {
      const user = await fillDobAndSelect(LABEL_MEDICAID)
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      await user.type(input, '1234567')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalled()
      })
    })

    it('accepts an 8-digit Medicaid ID (upper bound)', async () => {
      const user = await fillDobAndSelect(LABEL_MEDICAID)
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      await user.type(input, '12345678')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalled()
      })
    })

    it('rejects a 9-digit Medicaid ID (no SSN bleed-through)', async () => {
      // Guard against accidental reuse of the 9-digit SSN/ITIN rule for
      // medicaidId. With maxLength=8 a user can't even type a 9th digit,
      // so the realistic boundary check is: when 9 digits are programmatically
      // pasted, the form either truncates (caps at 8) or rejects on submit.
      // Either way, submit must not fire with a 9-digit medicaidId.
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = await fillDobAndSelect(LABEL_MEDICAID)
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      await user.type(input, '123456789')
      // Expect the 9th digit to be clipped by maxLength=8.
      expect(input).not.toHaveValue('123456789')

      // Now double-check: if maxLength were raised, the form would still block
      // submission. We can't easily simulate a paste past maxLength, but the
      // prior expect already proves 9 chars never land in state via typing.
      await user.click(screen.getByRole('button', { name: /continue/i }))

      // 8-digit values submit successfully; the purpose of this test is to
      // show a typed 9-digit entry can't land as-is. If the user actually
      // ends up with 8 chars after typing 9, that's a valid submission.
      if ((input as HTMLInputElement).value.length === 8) {
        await waitFor(() => {
          expect(mockPush).toHaveBeenCalled()
        })
      } else {
        expect(submitCalled).toBe(false)
      }
    })

    it('strips non-digit characters from Medicaid ID input', async () => {
      const user = await fillDobAndSelect(LABEL_MEDICAID)
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      await user.type(input, '12-34-567')
      expect(input).toHaveValue('1234567')
    })

    it('sets inputMode="numeric" and maxLength=8 on the Medicaid ID input', async () => {
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )
      await user.click(screen.getByRole('radio', { name: LABEL_MEDICAID }))
      const input = await screen.findByRole('textbox', { name: INPUT_LABEL_MEDICAID })
      expect(input).toHaveAttribute('inputMode', 'numeric')
      expect(input).toHaveAttribute('maxLength', '8')
    })

    it('rejects a 6-digit SNAP/TANF account ID (shared 7-8 rule, representative case)', async () => {
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = await fillDobAndSelect('SNAP or TANF account ID')
      const input = await screen.findByRole('textbox', {
        name: /Enter your SNAP or TANF account ID/i
      })
      await user.type(input, '123456')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(submitCalled).toBe(false)
    })

    it('rejects a 6-digit SNAP/TANF person ID (shared 7-8 rule, representative case)', async () => {
      let submitCalled = false
      server.use(
        http.post('/api/id-proofing', () => {
          submitCalled = true
          return HttpResponse.json({ result: 'matched' })
        })
      )

      const user = await fillDobAndSelect('SNAP or TANF person ID')
      const input = await screen.findByRole('textbox', {
        name: /Enter your SNAP or TANF person ID/i
      })
      await user.type(input, '123456')
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        const errors = screen.getAllByRole('alert')
        expect(errors.length).toBeGreaterThanOrEqual(1)
      })
      expect(submitCalled).toBe(false)
    })
  })

  describe('co-loaded failure analytics', () => {
    // Override /auth/status to mark the session as co-loaded; AuthProvider reads this
    // on mount and makes session.isCoLoaded=true available via useAuth.
    function useCoLoadedSession() {
      server.use(
        http.get('/api/auth/status', () => {
          return HttpResponse.json({
            isAuthorized: true,
            email: 'co-loaded@example.com',
            ial: '1',
            idProofingStatus: 0,
            idProofingCompletedAt: null,
            idProofingExpiresAt: null,
            isCoLoaded: true
          })
        })
      )
    }

    it('tags idv_primary_reason as "not_found" for a co-loaded failed submission and fires result event exactly once', async () => {
      useCoLoadedSession()
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      // Wait for AuthProvider to settle the session so isCoLoaded reads true.
      await waitFor(() => {
        // Use the radio presence as a proxy for the form being interactive after
        // the provider resolves. This avoids querying internal state.
        expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
      })

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding?reason=noIdProvided')
      })

      expect(mockSetPageData).toHaveBeenCalledWith('idv_primary_status', 'fail')
      expect(mockSetPageData).toHaveBeenCalledWith('idv_primary_reason', 'not_found')
      // Exactly one IDV_PRIMARY_RESULT event per attempt (plus the IDV_PRIMARY_START event).
      const resultCalls = mockTrackEvent.mock.calls.filter(
        ([name]) => name === 'idv_primary_result'
      )
      expect(resultCalls).toHaveLength(1)
    })

    it('tags idv_primary_reason as "socure_fail" for a non-co-loaded failed submission', async () => {
      // Default /auth/status handler returns no isCoLoaded (null/undefined → false).
      const user = userEvent.setup()
      renderWithProviders(
        <IdProofingForm
          idOptions={TEST_ID_OPTIONS}
          contactLink={TEST_CONTACT_LINK}
        />
      )

      await waitFor(() => {
        expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
      })

      await user.selectOptions(screen.getByRole('combobox', { name: /month/i }), '06')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_DAY }), '20')
      await user.type(screen.getByRole('textbox', { name: INPUT_LABEL_YEAR }), '1985')
      await user.click(screen.getByRole('radio', { name: LABEL_NONE }))
      await user.click(screen.getByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding?reason=noIdProvided')
      })

      expect(mockSetPageData).toHaveBeenCalledWith('idv_primary_reason', 'socure_fail')
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
        expect(screen.getByRole('alert')).toHaveTextContent('An error occurred on our end')
      })
      expect(mockPush).not.toHaveBeenCalled()
    })
  })
})
