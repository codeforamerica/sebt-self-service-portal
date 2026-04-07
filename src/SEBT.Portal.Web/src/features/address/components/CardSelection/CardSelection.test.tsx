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

  // --- Successful submission ---

  it('redirects to dashboard with both params when cards selected', async () => {
    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Sophia Martinez/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    await user.click(checkboxes[0]!)

    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)

    expect(mockPush).toHaveBeenCalledWith('/dashboard?addressUpdated=true&cardsRequested=true')
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

  // --- Key uniqueness ---

  it('renders distinct checkboxes when multiple applications have null applicationNumber', async () => {
    server.use(
      http.get('/api/household/data', () => {
        return HttpResponse.json({
          email: 'test@example.com',
          phone: '3035550100',
          benefitIssuanceType: 1,
          applications: [
            {
              applicationNumber: null,
              applicationStatus: 'Approved',
              children: [{ firstName: 'Alice', lastName: 'Smith' }],
              childrenOnApplication: 1
            },
            {
              applicationNumber: null,
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

    const { user } = renderCardSelection()

    await waitFor(() => {
      expect(screen.getByText(/Alice Smith/)).toBeInTheDocument()
      expect(screen.getByText(/Bob Smith/)).toBeInTheDocument()
    })

    const checkboxes = screen.getAllByRole('checkbox')
    expect(checkboxes).toHaveLength(2)

    // Select only the first child — second should remain unchecked
    await user.click(checkboxes[0]!)
    expect(checkboxes[0]).toBeChecked()
    expect(checkboxes[1]).not.toBeChecked()
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
