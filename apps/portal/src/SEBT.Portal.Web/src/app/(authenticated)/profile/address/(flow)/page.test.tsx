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
  AddressForm: ({
    initialAddress,
    redirectPath
  }: {
    initialAddress: unknown
    redirectPath?: string
  }) => (
    <div
      data-testid="address-form"
      data-redirect-path={redirectPath ?? ''}
    >
      {initialAddress ? 'has-address' : 'no-address'}
    </div>
  )
}))

vi.mock('@/hooks/useFlowStartAnalytics', () => ({
  useFlowStartAnalytics: vi.fn()
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

  it('skips replacement-card prompt when canRequestReplacementCard is false', () => {
    mockHouseholdData = makeHousehold({
      allowedActions: {
        canUpdateAddress: true,
        canRequestReplacementCard: false,
        addressUpdateDeniedMessageKey: null,
        cardReplacementDeniedMessageKey: null
      }
    })
    render(<AddressFormPage />)

    expect(screen.getByTestId('address-form')).toHaveAttribute(
      'data-redirect-path',
      '/dashboard?addressUpdated=true'
    )
  })

  it('renders form when allowedActions is not provided (backward-compatible default)', () => {
    mockHouseholdData = makeHousehold()
    render(<AddressFormPage />)

    expect(screen.getByTestId('address-form')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })
})
