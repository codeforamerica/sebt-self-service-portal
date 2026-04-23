import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'

import CoLoadedInfoPage from './page'

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

vi.mock('@/features/address/components/CoLoadedInfo', () => ({
  CoLoadedInfo: () => <div data-testid="co-loaded-info">CoLoadedInfo content</div>
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

describe('CoLoadedInfoPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockState = 'dc'
    mockHouseholdData = null
    mockIsLoading = false
  })

  it('renders CoLoadedInfo content for co-loaded DC household (SNAP)', () => {
    mockState = 'dc'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedInfoPage />)

    expect(screen.getByTestId('co-loaded-info')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('renders CoLoadedInfo content for co-loaded DC household (TANF)', () => {
    mockState = 'dc'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'TanfEbtCard' })
    render(<CoLoadedInfoPage />)

    expect(screen.getByTestId('co-loaded-info')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects non-co-loaded DC households to /profile (SummerEbt benefit)', () => {
    // A DC household denied address update for reasons other than co-loaded
    // (e.g. Pending/Denied application status). The info page's FIS/SNAP-TANF
    // guidance does not apply — send the user back to the dashboard, where the
    // generic "self-service unavailable" alert explains the denial.
    mockState = 'dc'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SummerEbt' })
    render(<CoLoadedInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile')
    expect(screen.queryByTestId('co-loaded-info')).not.toBeInTheDocument()
  })

  it('redirects non-co-loaded DC households to /profile (Unknown benefit)', () => {
    mockState = 'dc'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'Unknown' })
    render(<CoLoadedInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile')
    expect(screen.queryByTestId('co-loaded-info')).not.toBeInTheDocument()
  })

  it('redirects non-DC users to dashboard', () => {
    mockState = 'co'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SummerEbt' })
    render(<CoLoadedInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/dashboard')
    expect(screen.queryByTestId('co-loaded-info')).not.toBeInTheDocument()
  })

  it('shows loading state (no redirect) while household data is fetching', () => {
    mockState = 'dc'
    mockHouseholdData = null
    mockIsLoading = true
    render(<CoLoadedInfoPage />)

    expect(mockReplace).not.toHaveBeenCalled()
    expect(screen.queryByTestId('co-loaded-info')).not.toBeInTheDocument()
  })
})
