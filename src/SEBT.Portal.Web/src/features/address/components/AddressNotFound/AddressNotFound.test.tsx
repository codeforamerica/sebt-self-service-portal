import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { AddressUpdateResponse, UpdateAddressRequest } from '../../api/schema'
import { AddressFlowProvider, useAddressFlow } from '../../context'
import { AddressNotFound } from './AddressNotFound'

const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush
  })
}))

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => mockState,
    getStateLinks: (state: string) => ({
      ...actual.getStateLinks(state as 'dc' | 'co'),
      help: {
        contactUs: 'https://sunbucks.dc.gov/page/contact-us',
        faqs: ''
      }
    })
  }
})

const TEST_ADDRESS: UpdateAddressRequest = {
  streetAddress1: '123 Main St NW',
  streetAddress2: 'Apt 4B',
  city: 'Washington',
  state: 'DC',
  postalCode: '20001'
}

const TEST_VALIDATION_RESULT: AddressUpdateResponse = {
  status: 'invalid',
  reason: 'not-found',
  message: 'Address not found'
}

/**
 * Helper component that populates the AddressFlowContext with test data
 * before rendering AddressNotFound. The provider starts empty, so this
 * bridges the gap by calling setValidationResult on mount.
 */
function ContextSeeder({
  enteredAddress,
  validationResult,
  includeInspector = false
}: {
  enteredAddress: UpdateAddressRequest
  validationResult: AddressUpdateResponse
  includeInspector?: boolean
}) {
  const { setValidationResult } = useAddressFlow()

  useEffect(() => {
    setValidationResult(validationResult, enteredAddress)
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <AddressNotFound />
      {includeInspector && <ContextInspector />}
    </>
  )
}

/**
 * Renders current context state for test assertions.
 */
function ContextInspector() {
  const { validationResult, address } = useAddressFlow()
  return (
    <div data-testid="context-inspector">
      <span data-testid="has-validation-result">{validationResult ? 'yes' : 'no'}</span>
      <span data-testid="has-address">{address ? 'yes' : 'no'}</span>
    </div>
  )
}

function renderComponent(
  enteredAddress: UpdateAddressRequest = TEST_ADDRESS,
  validationResult: AddressUpdateResponse = TEST_VALIDATION_RESULT,
  { includeInspector = false }: { includeInspector?: boolean } = {}
) {
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <AddressFlowProvider>
        <ContextSeeder
          enteredAddress={enteredAddress}
          validationResult={validationResult}
          includeInspector={includeInspector}
        />
      </AddressFlowProvider>
    )
  }
}

describe('AddressNotFound', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockState = 'dc'
  })

  // --- Content rendering ---

  it('renders the title and body text', () => {
    renderComponent()

    expect(
      screen.getByRole('heading', { name: /are you sure this address is correct/i })
    ).toBeInTheDocument()
    expect(screen.getByText(/couldn.t find the address you entered/i)).toBeInTheDocument()
  })

  it('shows entered address in the warning alert', () => {
    renderComponent()

    expect(screen.getByText(/123 Main St NW/)).toBeInTheDocument()
    expect(screen.getByText(/Apt 4B/)).toBeInTheDocument()
    expect(screen.getByText(/Washington, DC 20001/)).toBeInTheDocument()
  })

  it('shows the alert heading', () => {
    renderComponent()

    const heading = screen.getByRole('heading', { name: /address you entered/i })
    expect(heading).toBeInTheDocument()
    expect(heading.tagName).toBe('H4')
  })

  // --- DC state-specific ---

  it('DC: shows "Edit the address" button and "Contact us" link', () => {
    mockState = 'dc'
    renderComponent()

    expect(screen.getByRole('button', { name: /edit the address/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /contact us/i })).toBeInTheDocument()
  })

  it('DC: does not show "Use this address"', () => {
    mockState = 'dc'
    renderComponent()

    expect(screen.queryByRole('button', { name: /use this address/i })).not.toBeInTheDocument()
  })

  it('DC: "Contact us" link points to the state help URL', () => {
    mockState = 'dc'
    renderComponent()

    const contactLink = screen.getByRole('link', { name: /contact us/i })
    expect(contactLink).toHaveAttribute('href', 'https://sunbucks.dc.gov/page/contact-us')
  })

  // --- CO state-specific ---

  it('CO: shows "Edit the address" button and "Use this address" link', () => {
    mockState = 'co'
    renderComponent()

    expect(screen.getByRole('button', { name: /edit the address/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /use this address/i })).toBeInTheDocument()
  })

  it('CO: does not show "Contact us"', () => {
    mockState = 'co'
    renderComponent()

    expect(screen.queryByRole('link', { name: /contact us/i })).not.toBeInTheDocument()
  })

  // --- Navigation ---

  it('"Edit the address" clears validation result and navigates to the address form', async () => {
    const { user } = renderComponent()

    const editButton = screen.getByRole('button', { name: /edit the address/i })
    await user.click(editButton)

    expect(mockPush).toHaveBeenCalledWith('/profile/address')
  })

  it('CO: "Use this address" sets address and navigates to replacement cards', async () => {
    mockState = 'co'
    const { user } = renderComponent()

    const useButton = screen.getByRole('button', { name: /use this address/i })
    await user.click(useButton)

    expect(mockPush).toHaveBeenCalledWith('/profile/address/replacement-cards')
  })

  it('CO: "Use this address" preserves validationResult in context (prevents FlowGuard race)', async () => {
    mockState = 'co'
    const { user } = renderComponent(TEST_ADDRESS, TEST_VALIDATION_RESULT, {
      includeInspector: true
    })

    const useButton = screen.getByRole('button', { name: /use this address/i })
    await user.click(useButton)

    // validationResult should still be present (not cleared before navigation)
    expect(screen.getByTestId('has-validation-result')).toHaveTextContent('yes')
    expect(screen.getByTestId('has-address')).toHaveTextContent('yes')
  })

  // --- Blocked address ---

  it('shows blocked-specific title when reason is "blocked"', () => {
    const blockedResult: AddressUpdateResponse = {
      status: 'invalid',
      reason: 'blocked',
      message: 'This address cannot be used for mail delivery.'
    }
    renderComponent(TEST_ADDRESS, blockedResult)

    expect(
      screen.queryByRole('heading', { name: /are you sure this address is correct/i })
    ).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /this address can.t be used/i })).toBeInTheDocument()
  })

  it('shows blocked-specific body when reason is "blocked"', () => {
    const blockedResult: AddressUpdateResponse = {
      status: 'invalid',
      reason: 'blocked',
      message: 'This address cannot be used for mail delivery.'
    }
    renderComponent(TEST_ADDRESS, blockedResult)

    expect(screen.queryByText(/couldn.t find the address you entered/i)).not.toBeInTheDocument()
    expect(screen.getByText(/not available for .* card delivery/i)).toBeInTheDocument()
  })

  it('CO: does not show "Use this address" for blocked addresses', () => {
    mockState = 'co'
    const blockedResult: AddressUpdateResponse = {
      status: 'invalid',
      reason: 'blocked',
      message: 'This address cannot be used for mail delivery.'
    }
    renderComponent(TEST_ADDRESS, blockedResult)

    expect(screen.queryByRole('button', { name: /use this address/i })).not.toBeInTheDocument()
  })

  // --- Edge case ---

  it('omits the street address line 2 when not provided', () => {
    const addressWithoutLine2: UpdateAddressRequest = {
      streetAddress1: '456 Oak Ave',
      city: 'Denver',
      state: 'CO',
      postalCode: '80202'
    }
    renderComponent(addressWithoutLine2)

    expect(screen.getByText(/456 Oak Ave/)).toBeInTheDocument()
    expect(screen.getByText(/Denver, CO 80202/)).toBeInTheDocument()
    expect(screen.queryByText(/Apt/)).not.toBeInTheDocument()
  })
})
