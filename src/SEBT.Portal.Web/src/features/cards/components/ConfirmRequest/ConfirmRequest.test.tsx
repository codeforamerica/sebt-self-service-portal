import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { server } from '@/mocks/server'

import { ConfirmRequest } from './ConfirmRequest'

const mockPush = vi.fn()
const mockBack = vi.fn()

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
    cardMailedAt: '2026-01-03T00:00:00Z',
    cardActivatedAt: '2026-01-08T00:00:00Z',
    cardDeactivatedAt: null,
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
    cardMailedAt: '2026-01-03T00:00:00Z',
    cardActivatedAt: '2026-01-08T00:00:00Z',
    cardDeactivatedAt: null,
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
    mockState = 'dc'
  })

  // --- Content rendering ---

  it('renders the state-specific title for DC', () => {
    renderConfirmRequest()
    expect(screen.getByText(/DC SUN Bucks/)).toBeInTheDocument()
  })

  it('renders the state-specific title for CO', () => {
    mockState = 'co'
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
    renderConfirmRequest()
    expect(screen.getAllByText(/1234 \(last 4 digits\)/)).toHaveLength(2)
  })

  it('does not show card number in summary for DC', () => {
    mockState = 'dc'
    renderConfirmRequest()
    expect(screen.queryByText(/last 4 digits/i)).not.toBeInTheDocument()
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
