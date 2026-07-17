import { i18n } from '@sebt/design-system/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import amDcDashboard from '@/content/locales/am/dc/dashboard.json'
import enCoOptionalId from '@/content/locales/en/co/optionalId.json'
import enCoResult from '@/content/locales/en/co/result.json'
import enDcDashboard from '@/content/locales/en/dc/dashboard.json'
import enDcOptionalId from '@/content/locales/en/dc/optionalId.json'
import enDcResult from '@/content/locales/en/dc/result.json'
import esDcDashboard from '@/content/locales/es/dc/dashboard.json'
import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { server } from '@/mocks/server'
import { AnalyticsEvents } from '@sebt/analytics'

import { ConfirmRequest } from './ConfirmRequest'

const mockPush = vi.fn()
const mockBack = vi.fn()
const mockSetPageData = vi.fn()
const mockTrackEvent = vi.fn()

vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      setPageData: mockSetPageData,
      trackEvent: mockTrackEvent,
      pageLoad: vi.fn(),
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserData: vi.fn(),
      setUserProfile: vi.fn(),
      get: vi.fn()
    })
  }
})

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: mockBack
  })
}))

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => mockState
  }
})

// i18next's deep addResourceBundle mutates the bundle object already in its
// store, and initI18n seeds the store with the very module objects the JSON
// imports above resolve to. Snapshot pristine copies before any test runs and
// only ever hand clones to addResourceBundle, so a state swap can never
// corrupt the restore source.
const pristineBundles = {
  dc: { result: structuredClone(enDcResult), optionalId: structuredClone(enDcOptionalId) },
  co: { result: structuredClone(enCoResult), optionalId: structuredClone(enCoOptionalId) }
} as const

function loadStateBundles(state: keyof typeof pristineBundles) {
  const bundles = state === 'co' ? pristineBundles.co : pristineBundles.dc
  i18n.addResourceBundle('en', 'result', structuredClone(bundles.result), true, true)
  i18n.addResourceBundle('en', 'optionalId', structuredClone(bundles.optionalId), true, true)
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

const TEST_ADDRESS: Address = {
  streetAddress1: '123 Main St',
  streetAddress2: 'Apt 4B',
  city: 'Washington',
  state: 'DC',
  postalCode: '20001'
}

const TEST_CASES: SummerEbtCase[] = [
  {
    summerEBTCaseID: 'SEBT-001',
    childFirstName: 'Sophia',
    childLastName: 'Martinez',
    householdType: 'OSSE',
    eligibilityType: 'NSLP',
    issuanceType: 'SummerEbt',
    ebtCardLastFour: '1234',
    ebtCardStatus: 'Active',
    cardRequestedAt: '2026-01-01T00:00:00Z',
    allowAddressChange: true,
    allowCardReplacement: true
  },
  {
    summerEBTCaseID: 'SEBT-002',
    childFirstName: 'James',
    childLastName: 'Martinez',
    householdType: 'OSSE',
    eligibilityType: 'NSLP',
    issuanceType: 'SummerEbt',
    ebtCardLastFour: '1234',
    ebtCardStatus: 'Active',
    cardRequestedAt: '2026-01-01T00:00:00Z',
    allowAddressChange: true,
    allowCardReplacement: true
  }
]

function renderConfirmRequest(props?: {
  cases?: SummerEbtCase[]
  address?: Address
  onBack?: () => void
}) {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <ConfirmRequest
          cases={props?.cases ?? TEST_CASES}
          address={props?.address ?? TEST_ADDRESS}
          onBack={props?.onBack ?? mockBack}
        />
      </QueryClientProvider>
    )
  }
}

describe('ConfirmRequest', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockSetPageData.mockClear()
    mockTrackEvent.mockClear()
    mockState = 'dc'
    // Restore pristine DC bundles in case a prior test swapped to CO or overrode keys.
    loadStateBundles('dc')
  })

  // The i18n instance is a shared singleton; reset to English so sibling tests
  // (which assume English copy) aren't affected by a lingering language switch.
  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage('en')
    })
  })

  // --- Content rendering ---

  it('renders the state-specific title for DC', () => {
    renderConfirmRequest()
    expect(screen.getByText(/DC SUN Bucks/)).toBeInTheDocument()
  })

  it('renders the state-specific title for CO', () => {
    mockState = 'co'
    // Swap to CO bundles for this test; beforeEach restores DC for subsequent tests.
    loadStateBundles('co')
    renderConfirmRequest()
    expect(screen.getByText(/Summer EBT/)).toBeInTheDocument()
  })

  it('renders deactivation, delivery, and balance rollover bullets', () => {
    renderConfirmRequest()
    expect(screen.getByText(/permanently deactivated/i)).toBeInTheDocument()
    expect(screen.getByText(/7.?10 business days/i)).toBeInTheDocument()
    expect(screen.getByText(/rolled over/i)).toBeInTheDocument()
  })

  it('renders the card order summary with child names', () => {
    renderConfirmRequest()
    expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    expect(screen.getByText(/James Martinez/)).toBeInTheDocument()
  })

  it('renders the mailing address', () => {
    renderConfirmRequest()
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument()
    expect(screen.getByText(/Apt 4B/)).toBeInTheDocument()
    expect(screen.getByText(/Washington/)).toBeInTheDocument()
  })

  it('shows card number in summary for CO', () => {
    mockState = 'co'
    loadStateBundles('co')
    renderConfirmRequest()
    expect(screen.getAllByText(/1234 \(last 4 digits\)/)).toHaveLength(2)
  })

  it('does not show card number in summary for DC', () => {
    mockState = 'dc'
    renderConfirmRequest()
    expect(screen.queryByText(/last 4 digits/i)).not.toBeInTheDocument()
  })

  it('renders the child card lines from the optionalId who-is-card key', () => {
    // Sentinel template proves the component reads the translation key (with
    // [M.] stripped, since the case model has no middle name) instead of
    // hardcoding the English word order.
    i18n.addResourceBundle(
      'en',
      'optionalId',
      { "who'sCard": 'Card belonging to [First name] [M.] [Last name]' },
      true,
      true
    )
    renderConfirmRequest()
    expect(screen.getByText('Card belonging to Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('Card belonging to James Martinez')).toBeInTheDocument()
  })

  it('renders the card number lines from the optionalId cardNumber key for CO', () => {
    mockState = 'co'
    i18n.addResourceBundle(
      'en',
      'optionalId',
      { cardNumber: 'Número de tarjeta: [9999]' },
      true,
      true
    )
    renderConfirmRequest()
    expect(screen.getAllByText('Número de tarjeta: 1234')).toHaveLength(2)
  })

  it('renders the card list with visible bullets', () => {
    renderConfirmRequest()
    const list = screen.getByText(/Sophia Martinez/).closest('ul')
    expect(list).toHaveClass('usa-list')
    expect(list).not.toHaveClass('usa-list--unstyled')
  })

  // --- Single vs multi card copy ---

  it('uses plural copy for a multi-card order', () => {
    renderConfirmRequest()
    expect(screen.getByRole('button', { name: 'Order cards' })).toBeInTheDocument()
    expect(
      screen.getByText('New cards will be mailed to the following address:')
    ).toBeInTheDocument()
    expect(screen.getByText(/Once replacement cards are created/)).toBeInTheDocument()
  })

  it('uses singular copy for a single-card order', () => {
    renderConfirmRequest({ cases: [TEST_CASES[0]!] })
    expect(screen.getByRole('button', { name: 'Order card' })).toBeInTheDocument()
    expect(
      screen.getByText('Your new card will be mailed to the following address:')
    ).toBeInTheDocument()
    expect(screen.getByText(/Once a replacement card is created/)).toBeInTheDocument()
  })

  // --- Pre-title ---

  it('renders the pre-title for a single-card order (DC)', () => {
    renderConfirmRequest({ cases: [TEST_CASES[0]!] })
    expect(screen.getByText("Replace Sophia Martinez's card")).toBeInTheDocument()
  })

  it('renders the card-ending pre-title for a single-card order (CO)', () => {
    mockState = 'co'
    loadStateBundles('co')
    renderConfirmRequest({ cases: [TEST_CASES[0]!] })
    expect(screen.getByText('Replace card ending in 1234')).toBeInTheDocument()
  })

  it('does not render a pre-title for a multi-card order', () => {
    renderConfirmRequest()
    expect(screen.queryByText("Replace Sophia Martinez's card")).not.toBeInTheDocument()
    expect(screen.queryByText(/Replace card ending in/)).not.toBeInTheDocument()
  })

  it('skips the pre-title when a placeholder cannot be filled (CO without last 4)', () => {
    mockState = 'co'
    loadStateBundles('co')
    renderConfirmRequest({
      cases: [{ ...TEST_CASES[0]!, ebtCardLastFour: null }]
    })
    expect(screen.queryByText(/Replace card ending in/)).not.toBeInTheDocument()
    expect(screen.queryByText(/\[9999\]/)).not.toBeInTheDocument()
  })

  // --- Navigation ---

  it('calls onBack when back button is clicked', async () => {
    const onBack = vi.fn()
    const { user } = renderConfirmRequest({ onBack })

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(onBack).toHaveBeenCalled()
  })

  // --- Submission ---

  it('navigates to dashboard with flash param on successful submission', async () => {
    server.use(
      http.post('/api/household/cards/replace', () => {
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderConfirmRequest()

    const orderButton = screen.getByRole('button', { name: /order card/i })
    await user.click(orderButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/dashboard?flash=card_replaced')
    })
    expect(mockSetPageData).toHaveBeenCalledWith('card_replacement_status', 'success')
    expect(mockSetPageData).toHaveBeenCalledWith('error_code', null)
    expect(mockTrackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_SUBMIT)
    expect(mockTrackEvent).not.toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_ERROR)
  })

  it('shows error message when submission fails', async () => {
    server.use(
      http.post('/api/household/cards/replace', () => {
        return HttpResponse.json({ error: 'Cooldown active' }, { status: 400 })
      })
    )

    const { user } = renderConfirmRequest()

    const orderButton = screen.getByRole('button', { name: /order card/i })
    await user.click(orderButton)

    await waitFor(() => {
      expect(screen.getByText(/issue requesting/i)).toBeInTheDocument()
    })
    expect(mockSetPageData).toHaveBeenCalledWith('card_replacement_status', 'error')
    expect(mockSetPageData).toHaveBeenCalledWith('error_code', 'INVALID_INPUT')
    expect(mockTrackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_SUBMIT)
    expect(mockTrackEvent).toHaveBeenCalledWith(AnalyticsEvents.CARD_REPLACEMENT_ERROR)
  })

  it('re-translates the submit error across all DC languages, without resubmitting (DC-454)', async () => {
    server.use(
      http.post('/api/household/cards/replace', () =>
        HttpResponse.json({ error: 'Cooldown active' }, { status: 400 })
      )
    )
    const { user } = renderConfirmRequest()

    await user.click(screen.getByRole('button', { name: /order card/i }))
    expect(await screen.findByText(enDcDashboard.alertCardReplaceError)).toBeInTheDocument()

    // Cycle DC languages with the error on screen — no resubmit. afterEach resets to English.
    await act(async () => {
      await i18n.changeLanguage('es')
    })
    expect(await screen.findByText(esDcDashboard.alertCardReplaceError)).toBeInTheDocument()

    await act(async () => {
      await i18n.changeLanguage('am')
    })
    expect(await screen.findByText(amDcDashboard.alertCardReplaceError)).toBeInTheDocument()
    expect(screen.queryByText(enDcDashboard.alertCardReplaceError)).toBeNull()
  })

  it('sends caseRefs with applicationId/applicationStudentId from each case', async () => {
    let capturedBody: unknown = null
    server.use(
      http.post('/api/household/cards/replace', async ({ request }) => {
        capturedBody = await request.json()
        return new HttpResponse(null, { status: 204 })
      })
    )

    // First case: auto-eligible shape (no applicationId/applicationStudentId).
    // Second case: application-based shape with both populated.
    const cases: SummerEbtCase[] = [
      TEST_CASES[0]!,
      {
        ...TEST_CASES[1]!,
        applicationId: 'APP-2',
        applicationStudentId: 'STU-2'
      }
    ]

    const { user } = renderConfirmRequest({ cases })

    await user.click(screen.getByRole('button', { name: /order card/i }))

    await waitFor(() => expect(capturedBody).not.toBeNull())
    expect(capturedBody).toEqual({
      caseRefs: [
        {
          summerEbtCaseId: 'SEBT-001',
          applicationId: null,
          applicationStudentId: null
        },
        {
          summerEbtCaseId: 'SEBT-002',
          applicationId: 'APP-2',
          applicationStudentId: 'STU-2'
        }
      ]
    })
  })

  it('disables order button while submitting', async () => {
    let resolveRequest: () => void
    const pending = new Promise<void>((resolve) => {
      resolveRequest = resolve
    })

    server.use(
      http.post('/api/household/cards/replace', async () => {
        await pending
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderConfirmRequest()

    const orderButton = screen.getByRole('button', { name: /order card/i })
    await user.click(orderButton)

    expect(orderButton).toBeDisabled()

    resolveRequest!()
    await waitFor(() => {
      expect(mockPush).toHaveBeenCalled()
    })
  })
})
