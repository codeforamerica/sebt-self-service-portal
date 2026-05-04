import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import { CardSelection } from './CardSelection'

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
      queries: { retry: false }
    }
  })
}

function renderCardSelection() {
  const queryClient = createTestQueryClient()
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <CardSelection />
      </QueryClientProvider>
    )
  }
}

const TWO_CHILD_HOUSEHOLD = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  benefitIssuanceType: 1,
  summerEbtCases: [
    {
      summerEBTCaseID: 'SEBT-001',
      applicationId: 'APP-2026-001',
      childFirstName: 'Sophia',
      childLastName: 'Martinez',
      childDateOfBirth: '2015-06-15T00:00:00Z',
      householdType: 'SNAP',
      eligibilityType: 'Direct',
      ebtCaseNumber: 'CASE-100001',
      ebtCardLastFour: '1234',
      ebtCardStatus: 'Active',
      issuanceType: 1,
      benefitAvailableDate: '2026-01-08T00:00:00Z',
      benefitExpirationDate: '2026-09-30T00:00:00Z'
    },
    {
      summerEBTCaseID: 'SEBT-002',
      applicationId: 'APP-2026-001',
      childFirstName: 'James',
      childLastName: 'Martinez',
      childDateOfBirth: '2017-03-20T00:00:00Z',
      householdType: 'SNAP',
      eligibilityType: 'Direct',
      ebtCaseNumber: 'CASE-100001',
      ebtCardLastFour: '1234',
      ebtCardStatus: 'Active',
      issuanceType: 1,
      benefitAvailableDate: '2026-01-08T00:00:00Z',
      benefitExpirationDate: '2026-09-30T00:00:00Z'
    }
  ],
  applications: [
    {
      applicationNumber: 'APP-2026-001',
      caseNumber: 'CASE-DC-2026-001',
      applicationStatus: 'Approved',
      benefitIssueDate: '2026-01-08T00:00:00Z',
      benefitExpirationDate: '2026-03-19T00:00:00Z',
      last4DigitsOfCard: '1234',
      cardStatus: 'Active',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: '2026-01-03T00:00:00Z',
      cardActivatedAt: '2026-01-08T00:00:00Z',
      cardDeactivatedAt: null,
      issuanceType: 1,
      children: [
        { caseNumber: 456001, firstName: 'Sophia', lastName: 'Martinez' },
        { caseNumber: 456002, firstName: 'James', lastName: 'Martinez' }
      ],
      childrenOnApplication: 2
    }
  ],
  addressOnFile: {
    streetAddress1: '123 Main St',
    city: 'Washington',
    state: 'DC',
    postalCode: '20001'
  }
}

describe('CardSelection', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
  })

  // --- Rendering children ---

  it('renders checkboxes for each child', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
      expect(screen.getByText(/James Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(2)
  })

  // --- State-specific content ---

  it('shows card number for CO', async () => {
    mockState = 'co'
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getAllByText(/1234 \(last 4 digits\)/)).toHaveLength(2)
    })
  })

  it('does not show card number for DC', async () => {
    mockState = 'dc'
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/last 4 digits/)).not.toBeInTheDocument()
  })

  // --- Validation ---

  it('shows error when submitting without selection', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(screen.getByText(/select at least one/i)).toBeInTheDocument()
  })

  it('focuses error message on validation failure', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const errorMessage = screen.getByText(/select at least one/i)
    expect(errorMessage.closest('[tabindex="-1"]')).toHaveFocus()
  })

  it('links error message to fieldset via aria-describedby', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const fieldset = screen.getByRole('group', { name: /select which cards/i })
    expect(fieldset).toHaveAttribute('aria-describedby', expect.stringContaining('error'))
  })

  // --- Successful submission ---

  it('navigates to confirm page with selected case IDs', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(mockPush).toHaveBeenCalledWith(expect.stringContaining('select/confirm?cases='))
  })

  // --- Error handling ---

  it('shows error alert when household data fails to load', async () => {
    // 400 surfaces the error UI without retries (4xx skips retry); 401 is suppressed
    // by useHouseholdData while the SPA redirects to /login.
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Bad Request' }, { status: 400 })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/unable to load household members/i)).toBeInTheDocument()
    })
  })

  // --- Back button ---

  it('navigates back when back button is clicked', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockBack).toHaveBeenCalled()
  })

  // --- Null summerEBTCaseID filtering ---

  it('excludes cases without summerEBTCaseID', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'test@example.com',
          phone: '(303) 555-0100',
          benefitIssuanceType: 1,
          summerEbtCases: [
            {
              summerEBTCaseID: null,
              childFirstName: 'Alice',
              childLastName: 'Smith',
              householdType: 'SNAP',
              eligibilityType: 'Direct',
              issuanceType: 1,
              benefitAvailableDate: '2026-01-08T00:00:00Z',
              benefitExpirationDate: '2026-09-30T00:00:00Z'
            },
            {
              summerEBTCaseID: 'SEBT-002',
              childFirstName: 'Bob',
              childLastName: 'Smith',
              householdType: 'SNAP',
              eligibilityType: 'Direct',
              issuanceType: 1,
              benefitAvailableDate: '2026-01-08T00:00:00Z',
              benefitExpirationDate: '2026-09-30T00:00:00Z'
            }
          ],
          applications: [],
          addressOnFile: {
            streetAddress1: '123 Main St',
            city: 'Washington',
            state: 'DC',
            postalCode: '20001'
          }
        })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Bob Smith/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/Alice Smith/)).not.toBeInTheDocument()
    expect(screen.getAllByRole('checkbox')).toHaveLength(1)
  })

  // --- Accessibility ---

  it('uses fieldset and legend for checkbox group', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(TWO_CHILD_HOUSEHOLD)))

    renderCardSelection()

    await waitFor(() => {
      const fieldset = screen.getByRole('group', { name: /select which cards/i })
      expect(fieldset).toBeInTheDocument()
    })
  })

  // --- Co-loaded case filtering ---
  // issuanceType values: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
  // SNAP/TANF co-loaded cases cannot have a Summer EBT replacement card mailed,
  // so they must not appear in the "which cards to replace?" selection.

  it('excludes SNAP co-loaded cases from the selection', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'mixed@example.com',
          phone: '3035550100',
          benefitIssuanceType: 1,
          summerEbtCases: [
            {
              summerEBTCaseID: 'SEBT-SUMMER',
              childFirstName: 'Summer',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1
            },
            {
              summerEBTCaseID: 'SEBT-SNAP',
              childFirstName: 'Snap',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 3
            }
          ],
          applications: [],
          addressOnFile: {
            streetAddress1: '123 Main St',
            city: 'Washington',
            state: 'DC',
            postalCode: '20001'
          }
        })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Summer Child/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/Snap Child/)).not.toBeInTheDocument()

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(1)
  })

  it('excludes TANF co-loaded cases from the selection', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'mixed@example.com',
          phone: '3035550100',
          benefitIssuanceType: 1,
          summerEbtCases: [
            {
              summerEBTCaseID: 'SEBT-SUMMER',
              childFirstName: 'Summer',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1
            },
            {
              summerEBTCaseID: 'SEBT-TANF',
              childFirstName: 'Tanf',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 2
            }
          ],
          applications: [],
          addressOnFile: {
            streetAddress1: '123 Main St',
            city: 'Washington',
            state: 'DC',
            postalCode: '20001'
          }
        })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Summer Child/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/Tanf Child/)).not.toBeInTheDocument()

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(1)
  })

  it('excludes cases where allowCardReplacement is false (server-gated by SelfServiceRules card status)', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'mixed@example.com',
          phone: '3035550100',
          benefitIssuanceType: 1,
          summerEbtCases: [
            {
              summerEBTCaseID: 'SEBT-ELIGIBLE',
              childFirstName: 'Eligible',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1,
              allowCardReplacement: true
            },
            {
              summerEBTCaseID: 'SEBT-INELIGIBLE',
              childFirstName: 'Ineligible',
              childLastName: 'Child',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1,
              allowCardReplacement: false
            }
          ],
          applications: [],
          addressOnFile: {
            streetAddress1: '123 Main St',
            city: 'Washington',
            state: 'DC',
            postalCode: '20001'
          }
        })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Eligible Child/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/Ineligible Child/)).not.toBeInTheDocument()

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(1)
  })

  it('renders SummerEbt-only households unchanged', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'summeronly@example.com',
          phone: '3035550100',
          benefitIssuanceType: 1,
          summerEbtCases: [
            {
              summerEBTCaseID: 'SEBT-1',
              childFirstName: 'First',
              childLastName: 'Kid',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1
            },
            {
              summerEBTCaseID: 'SEBT-2',
              childFirstName: 'Second',
              childLastName: 'Kid',
              householdType: 'OSSE',
              eligibilityType: 'NSLP',
              issuanceType: 1
            }
          ],
          applications: [],
          addressOnFile: {
            streetAddress1: '123 Main St',
            city: 'Washington',
            state: 'DC',
            postalCode: '20001'
          }
        })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/First Kid/)).toBeInTheDocument()
      expect(screen.getByText(/Second Kid/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(2)
  })
})
