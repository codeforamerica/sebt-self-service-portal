import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import type { AddressUpdateResponse, UpdateAddressRequest } from '../../api/schema'
import { AddressFlowProvider, useAddressFlow } from '../../context'
import { SuggestedAddress } from './SuggestedAddress'

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

// --- Test fixtures ---

const mockSuggested = {
  streetAddress1: '123 MLK Jr Ave NW',
  streetAddress2: null,
  city: 'Washington',
  state: 'DC',
  postalCode: '20001'
}

const mockEntered: UpdateAddressRequest = {
  streetAddress1: '123 Martin Luther King Jr Ave NW',
  city: 'Washington',
  state: 'District of Columbia',
  postalCode: '20001'
}

const mockValidationResult: AddressUpdateResponse = {
  status: 'suggestion',
  reason: 'abbreviated',
  suggestedAddress: mockSuggested
}

const mockSuggestionResult: AddressUpdateResponse = {
  status: 'suggestion',
  reason: 'corrected',
  suggestedAddress: mockSuggested
}

// --- Helpers ---

/**
 * Pre-populates the address flow context with a validation result and entered address
 * so SuggestedAddress has data to render.
 */
function ContextSetter({
  children,
  result,
  entered,
  formPath,
  continuePath
}: {
  children: ReactNode
  result: AddressUpdateResponse
  entered: UpdateAddressRequest
  formPath?: string
  continuePath?: string
}) {
  const { setValidationResult, setNavigationTargets } = useAddressFlow()
  useEffect(() => {
    setValidationResult(result, entered)
    if (formPath && continuePath) {
      setNavigationTargets({ formPath, continuePath })
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps
  return <>{children}</>
}

/**
 * Renders current context state for test assertions. Allows tests to verify
 * whether validationResult was cleared or preserved after button clicks.
 */
function ContextInspector() {
  const { validationResult, address } = useAddressFlow()
  return (
    <div data-testid="context-inspector">
      <span data-testid="has-validation-result">{validationResult ? 'yes' : 'no'}</span>
      <span data-testid="has-address">{address ? 'yes' : 'no'}</span>
      <span data-testid="address-json">{address ? JSON.stringify(address) : ''}</span>
    </div>
  )
}

function renderSuggestedAddress(
  result: AddressUpdateResponse = mockSuggestionResult,
  entered: UpdateAddressRequest = mockEntered,
  {
    includeInspector = false,
    formPath,
    continuePath
  }: { includeInspector?: boolean; formPath?: string; continuePath?: string } = {}
) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <QueryClientProvider client={queryClient}>
        <AddressFlowProvider>
          <ContextSetter
            result={result}
            entered={entered}
            {...(formPath ? { formPath } : {})}
            {...(continuePath ? { continuePath } : {})}
          >
            <SuggestedAddress />
            {includeInspector && <ContextInspector />}
          </ContextSetter>
        </AddressFlowProvider>
      </QueryClientProvider>
    )
  }
}

describe('SuggestedAddress', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
  })

  // --- Rendering (suggested variant) ---

  it('renders title and body text', () => {
    renderSuggestedAddress()

    expect(screen.getByRole('heading', { name: /check the address/i })).toBeInTheDocument()
    expect(screen.getByText(/we updated the address you entered/i)).toBeInTheDocument()
  })

  it('renders radio group with "Suggested address" pre-selected', () => {
    renderSuggestedAddress()

    const suggestedRadio = screen.getByRole('radio', { name: /suggested address/i })
    expect(suggestedRadio).toBeChecked()
  })

  it('shows suggested address details in the radio option', () => {
    renderSuggestedAddress()

    expect(screen.getByText(/123 MLK Jr Ave NW/)).toBeInTheDocument()
    expect(screen.getByText(/Washington, DC 20001/)).toBeInTheDocument()
  })

  it('shows entered address details in the radio option', () => {
    renderSuggestedAddress()

    expect(screen.getByText(/123 Martin Luther King Jr Ave NW/)).toBeInTheDocument()
    expect(screen.getByText(/Washington, District of Columbia 20001/)).toBeInTheDocument()
  })

  // --- Selection ---

  it('switches selection when "Address you entered" is clicked', async () => {
    const { user } = renderSuggestedAddress()

    const enteredRadio = screen.getByRole('radio', { name: /address you entered/i })
    await user.click(enteredRadio)

    expect(enteredRadio).toBeChecked()
    expect(screen.getByRole('radio', { name: /suggested address/i })).not.toBeChecked()
  })

  // --- Continue button ---

  it('calls setAddress with suggested address and navigates to replacement-cards', async () => {
    server.use(
      http.put('/api/household/address', async ({ request }) => {
        const body = (await request.json()) as UpdateAddressRequest
        expect(body).toMatchObject({
          streetAddress1: '123 MLK Jr Ave NW',
          city: 'Washington',
          state: 'DC',
          postalCode: '20001'
        })
        return HttpResponse.json({ status: 'valid' })
      })
    )

    const { user } = renderSuggestedAddress()

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  it('calls API with acceptEnteredAddress when "Address you entered" is selected', async () => {
    server.use(
      http.put('/api/household/address', async ({ request }) => {
        const body = (await request.json()) as UpdateAddressRequest
        expect(body.acceptEnteredAddress).toBe(true)
        expect(body).toMatchObject({
          streetAddress1: '123 Martin Luther King Jr Ave NW',
          city: 'Washington',
          state: 'District of Columbia',
          postalCode: '20001'
        })
        return HttpResponse.json({
          status: 'valid',
          normalizedAddress: {
            streetAddress1: body.streetAddress1,
            streetAddress2: body.streetAddress2 ?? null,
            city: body.city,
            state: body.state,
            postalCode: body.postalCode
          }
        })
      })
    )

    const { user } = renderSuggestedAddress()

    const enteredRadio = screen.getByRole('radio', { name: /address you entered/i })
    await user.click(enteredRadio)

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  it('navigates using context continuePath when configured', async () => {
    server.use(http.put('/api/household/address', () => HttpResponse.json({ status: 'valid' })))
    const { user } = renderSuggestedAddress(mockSuggestionResult, mockEntered, {
      formPath: '/cards/replace/address?case=SEBT-001',
      continuePath: '/cards/replace/confirm?case=SEBT-001'
    })

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/cards/replace/confirm?case=SEBT-001')
    })
  })

  it('preserves validationResult in context when Continue is clicked (prevents FlowGuard race)', async () => {
    server.use(http.put('/api/household/address', () => HttpResponse.json({ status: 'valid' })))

    const { user } = renderSuggestedAddress(mockSuggestionResult, mockEntered, {
      includeInspector: true
    })

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      // validationResult should still be present after Continue (not cleared)
      expect(screen.getByTestId('has-validation-result')).toHaveTextContent('yes')
      // address should now be set
      expect(screen.getByTestId('has-address')).toHaveTextContent('yes')
    })
  })

  it('stores normalized address from API response when suggested address is accepted', async () => {
    server.use(
      http.put('/api/household/address', () =>
        HttpResponse.json({
          status: 'valid',
          normalizedAddress: {
            streetAddress1: '123 MLK JR AVE NW',
            streetAddress2: null,
            city: 'WASHINGTON',
            state: 'DC',
            postalCode: '20001-0001'
          }
        })
      )
    )

    const { user } = renderSuggestedAddress(mockSuggestionResult, mockEntered, {
      includeInspector: true
    })

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(screen.getByTestId('has-address')).toHaveTextContent('yes')
      const contextData = JSON.parse(screen.getByTestId('address-json').textContent ?? '{}')
      expect(contextData.streetAddress1).toBe('123 MLK JR AVE NW')
      expect(contextData.city).toBe('WASHINGTON')
      expect(contextData.postalCode).toBe('20001-0001')
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  it('continues when API returns suggestion after selecting suggested address', async () => {
    server.use(
      http.put('/api/household/address', () =>
        HttpResponse.json({
          status: 'suggestion',
          reason: 'suggested',
          suggestedAddress: {
            streetAddress1: '123 MLK JR AVE NW',
            streetAddress2: null,
            city: 'WASHINGTON',
            state: 'DC',
            postalCode: '20001-0001'
          }
        })
      )
    )

    const { user } = renderSuggestedAddress(mockSuggestionResult, mockEntered, {
      includeInspector: true
    })

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(screen.getByTestId('has-address')).toHaveTextContent('yes')
      const contextData = JSON.parse(screen.getByTestId('address-json').textContent ?? '{}')
      expect(contextData.streetAddress1).toBe('123 MLK JR AVE NW')
      expect(contextData.city).toBe('WASHINGTON')
      expect(contextData.postalCode).toBe('20001-0001')
      expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
    })
  })

  it('shows an error and stays on the page when persisting the suggested address fails', async () => {
    server.use(
      http.put('/api/household/address', () =>
        HttpResponse.json({ error: 'Bad request' }, { status: 400 })
      )
    )

    const { user } = renderSuggestedAddress()

    const continueButton = screen.getByRole('button', { name: /continue/i })
    await user.click(continueButton)

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument()
    })
    expect(mockPush).not.toHaveBeenCalled()
  })

  // --- Back button ---

  it('navigates to /profile/address when back button is clicked', async () => {
    const { user } = renderSuggestedAddress()

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockPush).toHaveBeenCalledWith('/profile/address')
  })

  it('back button uses context formPath when configured', async () => {
    const { user } = renderSuggestedAddress(mockSuggestionResult, mockEntered, {
      formPath: '/cards/replace/address?case=SEBT-001',
      continuePath: '/cards/replace/confirm?case=SEBT-001'
    })

    const backButton = screen.getByRole('button', { name: /back/i })
    await user.click(backButton)

    expect(mockPush).toHaveBeenCalledWith('/cards/replace/address?case=SEBT-001')
  })

  // --- DC abbreviated variant ---

  it('renders abbreviated copy when reason is "abbreviated" and state is DC', () => {
    mockState = 'dc'
    renderSuggestedAddress(mockValidationResult, mockEntered)

    expect(
      screen.getByText(/we updated the street address to a format we can accept/i)
    ).toBeInTheDocument()
  })

  it('does not render abbreviated copy when state is not DC', () => {
    mockState = 'co'
    renderSuggestedAddress(mockValidationResult, mockEntered)

    // Should show the standard suggested body, not the abbreviated one
    expect(screen.getByText(/we updated the address you entered/i)).toBeInTheDocument()
    expect(
      screen.queryByText(/we updated the street address to a format we can accept/i)
    ).not.toBeInTheDocument()
  })

  it('renders the radio group legend with required indicator', () => {
    renderSuggestedAddress()

    expect(screen.getByText(/select the address to use/i)).toBeInTheDocument()
  })

  it('renders "Asterisks (*) indicate a required field" note', () => {
    renderSuggestedAddress()

    expect(screen.getByText(/asterisks .* indicate a required field/i)).toBeInTheDocument()
  })
})
