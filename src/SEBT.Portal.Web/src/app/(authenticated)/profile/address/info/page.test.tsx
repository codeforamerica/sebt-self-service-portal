import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'
import { createMockApplication } from '@/features/household/testing'

import CoLoadedAddressInfoPage from './page'

const mockReplace = vi.fn()
const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace,
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

let mockHouseholdData: HouseholdData | null = null
let mockIsLoading = false
let mockIsError = false
vi.mock('@/features/household', () => ({
  useHouseholdData: () => ({
    data: mockHouseholdData,
    isLoading: mockIsLoading,
    isError: mockIsError
  })
}))

const mockAuthSession: { isCoLoaded: boolean | null } = { isCoLoaded: false }
vi.mock('@/features/auth', () => ({
  useAuth: () => ({ session: mockAuthSession })
}))

const mockSetPageData = vi.fn()
vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      setPageData: mockSetPageData,
      setUserData: vi.fn(),
      trackEvent: vi.fn(),
      pageLoad: vi.fn(),
      get: vi.fn()
    })
  }
})

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

describe('CoLoadedAddressInfoPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
    mockHouseholdData = null
    mockIsLoading = false
    mockIsError = false
    mockAuthSession.isCoLoaded = false
    mockSetPageData.mockClear()
  })

  it('renders the SNAP/TANF mailing-address explainer for a co-loaded DC household (SNAP)', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    expect(
      screen.getByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
    ).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('renders the explainer for a co-loaded DC household (TANF)', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'TanfEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    expect(
      screen.getByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
    ).toBeInTheDocument()
  })

  it('links out to /cards/info for the replacement-card explainer', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    const replaceLink = screen.getByRole('link', {
      name: /tap here to learn how to get a replacement snap or tanf ebt card/i
    })
    expect(replaceLink).toHaveAttribute('href', '/cards/info')
  })

  it('links out to /contact for the contact-preferences flow', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    const contactLink = screen.getByRole('link', {
      name: /tap here to update your contact preferences/i
    })
    expect(contactLink).toHaveAttribute('href', '/contact')
  })

  it('redirects non-co-loaded DC households to /profile (SummerEbt benefit)', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SummerEbt' })
    render(<CoLoadedAddressInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile')
  })

  it('redirects non-co-loaded DC households to /profile (Unknown benefit)', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'Unknown' })
    render(<CoLoadedAddressInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile')
  })

  it('redirects non-DC users to /dashboard', () => {
    mockState = 'co'
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SummerEbt' })
    render(<CoLoadedAddressInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/dashboard')
  })

  it('shows loading state while household data is fetching', () => {
    mockHouseholdData = null
    mockIsLoading = true
    render(<CoLoadedAddressInfoPage />)

    expect(mockReplace).not.toHaveBeenCalled()
    expect(
      screen.queryByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
    ).not.toBeInTheDocument()
  })

  it('renders an error alert when the household fetch fails', () => {
    mockHouseholdData = null
    mockIsError = true
    render(<CoLoadedAddressInfoPage />)

    expect(
      screen.queryByRole('heading', { name: /mailing address for snap or tanf ebt card/i })
    ).not.toBeInTheDocument()
    expect(screen.getByText(/unable to load address details/i)).toBeInTheDocument()
  })

  it('tags the in-page CTAs with data-analytics-cta for click tracking', () => {
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    expect(
      screen.getByRole('link', {
        name: /tap here to learn how to get a replacement snap or tanf ebt card/i
      })
    ).toHaveAttribute('data-analytics-cta', 'address_info_to_cards_info_cta')
    expect(
      screen.getByRole('link', { name: /tap here to update your contact preferences/i })
    ).toHaveAttribute('data-analytics-cta', 'address_info_to_contact_cta')
  })

  it('sets household_type page data based on the household bucket', () => {
    mockAuthSession.isCoLoaded = true
    mockHouseholdData = makeHousehold({ benefitIssuanceType: 'SnapEbtCard' })
    render(<CoLoadedAddressInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'co_loaded_only')
  })

  it('tags household_type as mixed_eligibility when a co-loaded user also has a submitted application', () => {
    mockAuthSession.isCoLoaded = true
    mockHouseholdData = makeHousehold({
      benefitIssuanceType: 'SnapEbtCard',
      applications: [createMockApplication()]
    })
    render(<CoLoadedAddressInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'mixed_eligibility')
  })

  it('tags household_type as unknown when the household fetch fails', () => {
    mockHouseholdData = null
    mockIsError = true
    render(<CoLoadedAddressInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'unknown')
  })
})
