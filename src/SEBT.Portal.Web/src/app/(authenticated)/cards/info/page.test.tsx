import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'
import type { SummerEbtCase } from '@/features/household/api'
import { createMockSummerEbtCase } from '@/features/household/testing'

import CardInfoPage from './page'

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
vi.mock('@/features/household', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/household')>()
  return {
    ...actual,
    useHouseholdData: () => ({
      data: mockHouseholdData,
      isLoading: mockIsLoading,
      isError: mockIsError
    })
  }
})

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

function makeHousehold(cases: SummerEbtCase[]): HouseholdData {
  return {
    email: 'test@example.com',
    phone: null,
    summerEbtCases: cases,
    applications: [],
    addressOnFile: null,
    coLoadedCohort: 'NonCoLoaded'
  } as HouseholdData
}

describe('CardInfoPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
    mockHouseholdData = makeHousehold([])
    mockIsLoading = false
    mockIsError = false
    mockAuthSession.isCoLoaded = false
    mockSetPageData.mockClear()
  })

  it('renders the SNAP/TANF replacement-card explainer with DHS office locations', () => {
    render(<CardInfoPage />)

    expect(
      screen.getByRole('heading', { name: /getting a replacement snap or tanf ebt card/i })
    ).toBeInTheDocument()
    expect(screen.getByText(/dhs ebt card office locations/i)).toBeInTheDocument()
    expect(screen.getByText(/645 H Street NE, 2nd Floor/)).toBeInTheDocument()
    expect(screen.getByText(/1649 Marion Barry Avenue SE/)).toBeInTheDocument()
  })

  it('hides the "Go to the dashboard" alert when the household has no SUN Bucks card', () => {
    // Fully co-loaded household — no SummerEbt cases. The dashboard CTA the alert
    // points at would not exist, so the alert must be suppressed.
    mockHouseholdData = makeHousehold([createMockSummerEbtCase({ issuanceType: 'SnapEbtCard' })])
    render(<CardInfoPage />)

    expect(screen.queryByRole('link', { name: /go to the dashboard/i })).not.toBeInTheDocument()
  })

  it('shows the "Go to the dashboard" alert when the household has at least one SUN Bucks card', () => {
    // Mixed eligibility — alert tells the user to use the in-portal "Request a
    // replacement card" CTA on the dashboard for the SUN Bucks child.
    mockHouseholdData = makeHousehold([
      createMockSummerEbtCase({ issuanceType: 'SnapEbtCard' }),
      createMockSummerEbtCase({ issuanceType: 'SummerEbt' })
    ])
    render(<CardInfoPage />)

    const dashboardLink = screen.getByRole('link', { name: /go to the dashboard/i })
    expect(dashboardLink).toHaveAttribute('href', '/dashboard')
  })

  it('redirects non-DC users to /dashboard', () => {
    mockState = 'co'
    render(<CardInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/dashboard')
  })

  it('shows loading state while household data is fetching', () => {
    mockHouseholdData = null
    mockIsLoading = true
    render(<CardInfoPage />)

    expect(
      screen.queryByRole('heading', { name: /getting a replacement snap or tanf ebt card/i })
    ).not.toBeInTheDocument()
  })

  it('renders the FIS phone number as a tap-to-call link', () => {
    render(<CardInfoPage />)

    const fisLink = screen.getByRole('link', { name: /\(888\) 304-9167/ })
    expect(fisLink).toHaveAttribute('href', 'tel:+18883049167')
  })

  it('tags the FIS phone link as an external_only CTA for analytics', () => {
    render(<CardInfoPage />)

    const fisLink = screen.getByRole('link', { name: /\(888\) 304-9167/ })
    expect(fisLink).toHaveAttribute('data-analytics-cta', 'fis_phone_call')
    expect(fisLink).toHaveAttribute('data-analytics-cta-destination-type', 'external_only')
  })

  it('sets household_type page data based on the household bucket', () => {
    mockAuthSession.isCoLoaded = true
    mockHouseholdData = makeHousehold([createMockSummerEbtCase({ issuanceType: 'SnapEbtCard' })])
    render(<CardInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'co_loaded_only')
  })

  it('tags household_type as mixed_eligibility when a co-loaded user has at least one SummerEbt case', () => {
    mockAuthSession.isCoLoaded = true
    mockHouseholdData = makeHousehold([
      createMockSummerEbtCase({ issuanceType: 'SnapEbtCard' }),
      createMockSummerEbtCase({ issuanceType: 'SummerEbt' })
    ])
    render(<CardInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'mixed_eligibility')
  })

  it('tags household_type as unknown when the household fetch fails', () => {
    mockHouseholdData = null
    mockIsError = true
    render(<CardInfoPage />)

    expect(mockSetPageData).toHaveBeenCalledWith('household_type', 'unknown')
  })

  it('renders an error alert when the household fetch fails', () => {
    mockHouseholdData = null
    mockIsError = true
    render(<CardInfoPage />)

    expect(
      screen.queryByRole('heading', { name: /getting a replacement snap or tanf ebt card/i })
    ).not.toBeInTheDocument()
    expect(screen.getByText(/unable to load card details/i)).toBeInTheDocument()
  })

  it('exposes a Back button that calls router.back()', async () => {
    const user = (await import('@testing-library/user-event')).default.setup()
    render(<CardInfoPage />)

    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })
})
