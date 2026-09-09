import { i18n } from '@sebt/design-system/client'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData, SummerEbtCase } from '../../api'

import { EnrolledChildren } from './EnrolledChildren'

let mockApplyHref: string | null = '/apply'
vi.mock('@/lib/useApplyHref', () => ({
  useApplyHref: () => mockApplyHref
}))

const mockCase1: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-001',
  childFirstName: 'Sophia',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt',
  ebtCardLastFour: '1234',
  ebtCardStatus: 'Active',
  benefitAvailableDate: '2026-06-01T00:00:00Z',
  benefitExpirationDate: '2026-08-31T00:00:00Z',
  allowAddressChange: true,
  allowCardReplacement: true
}

const mockCase2: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-002',
  childFirstName: 'James',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt',
  allowAddressChange: true,
  allowCardReplacement: true
}

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '3035550100',
  summerEbtCases: [mockCase1, mockCase2],
  applications: [],
  addressOnFile: null,
  coLoadedCohort: 'NonCoLoaded'
}

let mockReturnData: HouseholdData

vi.mock('../../api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api')>()),
  useRequiredHouseholdData: () => mockReturnData
}))

describe('EnrolledChildren', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
    mockApplyHref = '/apply'
  })

  it('renders section heading', () => {
    render(<EnrolledChildren />)
    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('renders a child card for each case', () => {
    render(<EnrolledChildren />)
    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('James Martinez')).toBeInTheDocument()
  })

  it('expands only the first child by default', () => {
    render(<EnrolledChildren />)
    const buttons = screen.getAllByRole('button')
    expect(buttons[0]).toHaveAttribute('aria-expanded', 'true')
    expect(buttons[1]).toHaveAttribute('aria-expanded', 'false')
  })

  it('renders accordion with bordered variant', () => {
    const { container } = render(<EnrolledChildren />)
    const accordion = container.querySelector('.usa-accordion--bordered')
    expect(accordion).toBeInTheDocument()
  })

  it('renders children from multiple cases', () => {
    const case3: SummerEbtCase = {
      ...mockCase1,
      summerEBTCaseID: 'SEBT-003',
      childFirstName: 'Emily',
      childLastName: 'Brown'
    }
    mockReturnData = { ...defaultMockData, summerEbtCases: [mockCase1, case3] }
    render(<EnrolledChildren />)
    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })

  it('renders the apply link when applications are open and the action label is authored', () => {
    // The action key was dropped from the closed-season content (DC-701); an open
    // season restores it via the sheet. Simulate that restored state here.
    i18n.addResource('en', 'dashboard', 'sectionEnrolledChildrenAction', 'submit an application.')
    mockApplyHref = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    render(<EnrolledChildren />)
    expect(screen.getByRole('link', { name: /submit/i })).toHaveAttribute('href', mockApplyHref)
    // Reset to the closed-season shape (key empty) without dropping the whole bundle.
    i18n.addResource('en', 'dashboard', 'sectionEnrolledChildrenAction', '')
  })

  it('renders the intro sentence without an apply link when applications are closed', () => {
    // Applications are closed (DC-701): the intro is a complete sentence and stays;
    // only the link is gone.
    mockApplyHref = null
    render(<EnrolledChildren />)
    expect(
      screen.getByText('The following students are enrolled in DC SUN Bucks.')
    ).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /submit/i })).toBeNull()
    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
  })

  it('omits the apply link when the action label has no content even if applications are open', () => {
    // Guard against a raw key or empty anchor if the flag flips on before the
    // sheet restores the action copy.
    mockApplyHref = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    const { container } = render(<EnrolledChildren />)
    expect(container.querySelector(`a[href="${mockApplyHref}"]`)).toBeNull()
    expect(screen.queryByText('sectionEnrolledChildrenAction')).toBeNull()
  })
})
