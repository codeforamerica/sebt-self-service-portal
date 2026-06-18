import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { i18n } from '@sebt/design-system/client'

import amDcValidation from '@/content/locales/am/dc/validation.json'
import enDcValidation from '@/content/locales/en/dc/validation.json'
import esDcValidation from '@/content/locales/es/dc/validation.json'
import type { Address, SummerEbtCase } from '@/features/household/api/schema'

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

const TEST_CASE: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-001',
  childFirstName: 'Sophia',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt',
  ebtCardLastFour: '1234',
  ebtCardStatus: 'Active',
  cardRequestedAt: null,
  allowAddressChange: true,
  allowCardReplacement: true
}

function renderConfirmAddress() {
  const user = userEvent.setup()
  return {
    user,
    ...render(
      <ConfirmAddress
        summerEbtCase={TEST_CASE}
        address={TEST_ADDRESS}
        confirmPath="/cards/replace/confirm?case=SEBT-001"
        changePath="/cards/replace/address?case=SEBT-001"
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

  // The i18n instance is a shared singleton; reset to English so sibling tests
  // (which assume English labels) aren't affected by a lingering Spanish switch.
  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage('en')
    })
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

    expect(mockPush).toHaveBeenCalledWith('/cards/replace/confirm?case=SEBT-001')
  })

  it('navigates to change path when no is selected', async () => {
    const { user } = renderConfirmAddress()

    await user.click(screen.getByLabelText(/no/i))
    await user.click(screen.getByRole('button', { name: /continue/i }))

    expect(mockPush).toHaveBeenCalledWith('/cards/replace/address?case=SEBT-001')
  })

  it('re-translates the selection error across all DC languages, without resubmitting (DC-454)', async () => {
    const { user } = renderConfirmAddress()

    await user.click(screen.getByRole('button', { name: /continue/i }))
    // Scope to the alert: in Spanish/Amharic the legend (common:selectOne) and the error
    // (validation:selectOption) can render as the same text, so a free-text query would match
    // multiple elements. toHaveTextContent substring-matches within the alert only.
    expect(await screen.findByRole('alert')).toHaveTextContent(enDcValidation.selectOption)

    await act(async () => {
      await i18n.changeLanguage('es')
    })
    expect(await screen.findByRole('alert')).toHaveTextContent(esDcValidation.selectOption)

    await act(async () => {
      await i18n.changeLanguage('am')
    })
    expect(await screen.findByRole('alert')).toHaveTextContent(amDcValidation.selectOption)
  })

  it('navigates back when back button is clicked', async () => {
    const { user } = renderConfirmAddress()
    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })
})
