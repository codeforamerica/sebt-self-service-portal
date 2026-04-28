import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'

import AddressFormPage from './page'

const mockReplace = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace
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

vi.mock('@/features/address/components/AddressForm', () => ({
  AddressForm: ({ initialAddress }: { initialAddress: unknown }) => (
    <div data-testid="address-form">{initialAddress ? 'has-address' : 'no-address'}</div>
  )
}))

let mockHouseholdData: HouseholdData | null = null
let mockIsLoading = false
vi.mock('@/features/household', () => ({
  useHouseholdData: () => ({
    data: mockHouseholdData,
    isLoading: mockIsLoading,
    isError: false
  })
}))

function makeHousehold(partial: Partial<HouseholdData> = {}): HouseholdData {
  return {
    email: 'test@example.com',
    phone: null,
    summerEbtCases: [],
    applications: [],
    addressOnFile: null,
    ...partial
  } as HouseholdData
}

describe('AddressFormPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockState = 'dc'
    mockHouseholdData = null
    mockIsLoading = false
  })

  it('renders address form when canUpdateAddress is true', () => {
    mockHouseholdData = makeHousehold({
      allowedActions: {
        canUpdateAddress: true,
        canRequestReplacementCard: true,
        addressUpdateDeniedMessageKey: null,
        cardReplacementDeniedMessageKey: null
      }
    })
    render(<AddressFormPage />)

    expect(screen.getByTestId('address-form')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects DC users to co-loaded info page when canUpdateAddress is false', () => {
    mockState = 'dc'
    mockHouseholdData = makeHousehold({
      allowedActions: {
        canUpdateAddress: false,
        canRequestReplacementCard: false,
        addressUpdateDeniedMessageKey: 'actionNavigationSelfServiceUnavailable',
        cardReplacementDeniedMessageKey: null
      }
    })
    render(<AddressFormPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile/address/info')
  })

  it('redirects non-DC users to dashboard when canUpdateAddress is false', () => {
    mockState = 'co'
    mockHouseholdData = makeHousehold({
      allowedActions: {
        canUpdateAddress: false,
        canRequestReplacementCard: false,
        addressUpdateDeniedMessageKey: 'actionNavigationSelfServiceUnavailable',
        cardReplacementDeniedMessageKey: null
      }
    })
    render(<AddressFormPage />)

    expect(mockReplace).toHaveBeenCalledWith('/dashboard')
  })

  it('renders form when allowedActions is not provided (backward-compatible default)', () => {
    mockHouseholdData = makeHousehold()
    render(<AddressFormPage />)

    expect(screen.getByTestId('address-form')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })
})
