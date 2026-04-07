import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Address, Application } from '@/features/household/api/schema'

import { ConfirmAddress } from './ConfirmAddress'

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

const TEST_ADDRESS: Address = {
  streetAddress1: '123 Main St',
  streetAddress2: 'Apt 4B',
  city: 'Washington',
  state: 'DC',
  postalCode: '20001'
}

const TEST_APPLICATION: Application = {
  applicationNumber: 'APP-001',
  caseNumber: 'CASE-001',
  applicationStatus: 'Approved',
  benefitIssueDate: null,
  benefitExpirationDate: null,
  last4DigitsOfCard: '1234',
  cardStatus: 'Active',
  cardRequestedAt: null,
  cardMailedAt: null,
  cardActivatedAt: null,
  cardDeactivatedAt: null,
  children: [{ firstName: 'Sophia', lastName: 'Martinez' }],
  childrenOnApplication: 1,
  issuanceType: 'SummerEbt'
}

function renderConfirmAddress() {
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <ConfirmAddress
        application={TEST_APPLICATION}
        address={TEST_ADDRESS}
        confirmPath="/cards/replace/confirm?app=APP-001"
        changePath="/cards/replace/address?app=APP-001"
      />
    )
  }
}

describe('ConfirmAddress', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
  })

  it('renders child name subtitle for DC', () => {
    renderConfirmAddress()
    expect(screen.getByText(/Replace Sophia Martinez/)).toBeInTheDocument()
  })

  it('renders card number subtitle for CO', () => {
    mockState = 'co'
    renderConfirmAddress()
    expect(screen.getByText(/Replace card ending in 1234/)).toBeInTheDocument()
  })

  it('renders the address', () => {
    renderConfirmAddress()
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument()
    expect(screen.getByText(/Apt 4B/)).toBeInTheDocument()
  })

  it('shows error when submitting without selection', async () => {
    const { user } = renderConfirmAddress()
    const submitButton = screen.getByRole('button', { name: /continue/i })
    await user.click(submitButton)
    expect(screen.getByText(/select an option/i)).toBeInTheDocument()
  })

  it('navigates to confirm path when yes is selected', async () => {
    const { user } = renderConfirmAddress()

    await user.click(screen.getByLabelText(/yes/i))
    await user.click(screen.getByRole('button', { name: /continue/i }))

    expect(mockPush).toHaveBeenCalledWith('/cards/replace/confirm?app=APP-001')
  })

  it('navigates to change path when no is selected', async () => {
    const { user } = renderConfirmAddress()

    await user.click(screen.getByLabelText(/no/i))
    await user.click(screen.getByRole('button', { name: /continue/i }))

    expect(mockPush).toHaveBeenCalledWith('/cards/replace/address?app=APP-001')
  })

  it('navigates back when back button is clicked', async () => {
    const { user } = renderConfirmAddress()
    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })
})
