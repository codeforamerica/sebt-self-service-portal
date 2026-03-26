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

const MULTI_CHILD_HOUSEHOLD = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  benefitIssuanceType: 1,
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
    renderCardSelection()

    await waitFor(() => {
      expect(screen.getAllByText(/1234 \(last 4 digits\)/)).toHaveLength(2)
    })
  })

  it('does not show card number for DC', async () => {
    mockState = 'dc'
    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    expect(screen.queryByText(/last 4 digits/)).not.toBeInTheDocument()
  })

  // --- Validation ---

  it('shows error when submitting without selection', async () => {
    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(screen.getByText(/select at least one/i)).toBeInTheDocument()
  })

  it('focuses error message on validation failure', async () => {
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
    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    const fieldset = screen.getByRole('group', { name: /select which cards/i })
    expect(fieldset).toHaveAttribute('aria-describedby', expect.stringContaining('error'))
  })

  // --- Sibling auto-select (D6) ---

  it('selects all siblings when any child on an application is checked', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(MULTI_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    expect(checkboxes[0]).toBeChecked()
    expect(checkboxes[1]).toBeChecked()
  })

  it('disables sibling checkboxes when group is selected', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(MULTI_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    expect(checkboxes[0]).not.toBeDisabled()
    expect(checkboxes[1]).toBeDisabled()
  })

  it('shows shared card note on sibling checkboxes', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(MULTI_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    expect(screen.getByText(/share a card/i)).toBeInTheDocument()
  })

  it('deselects all siblings when first child is unchecked', async () => {
    server.use(http.get('/api/household/data', () => HttpResponse.json(MULTI_CHILD_HOUSEHOLD)))

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)
    await user.click(checkboxes[0]!)

    expect(checkboxes[0]).not.toBeChecked()
    expect(checkboxes[1]).not.toBeChecked()
  })

  // --- Successful submission ---

  it('navigates to confirm page with selected application numbers', async () => {
    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(mockPush).toHaveBeenCalledWith(expect.stringContaining('select/confirm?apps='))
  })

  // --- Error handling ---

  it('shows error alert when household data fails to load', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
      })
    )

    renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/unable to load household members/i)).toBeInTheDocument()
    })
  })

  // --- Back button ---

  it('navigates back when back button is clicked', async () => {
    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockBack).toHaveBeenCalled()
  })

  // --- Null applicationNumber filtering ---

  it('excludes applications without applicationNumber', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'test@example.com',
          phone: '(303) 555-0100',
          benefitIssuanceType: 1,
          applications: [
            {
              applicationNumber: null,
              applicationStatus: 'Approved',
              children: [{ firstName: 'Alice', lastName: 'Smith' }],
              childrenOnApplication: 1
            },
            {
              applicationNumber: 'APP-002',
              applicationStatus: 'Approved',
              children: [{ firstName: 'Bob', lastName: 'Smith' }],
              childrenOnApplication: 1
            }
          ],
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
    renderCardSelection()

    await waitFor(() => {
      const fieldset = screen.getByRole('group', { name: /select which cards/i })
      expect(fieldset).toBeInTheDocument()
    })
  })
})
