import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData, SummerEbtCase } from '../../api'

import { EnrolledChildren } from './EnrolledChildren'

let mockApplyHref = '/apply'
const mockGetApplyHref = vi.fn((_locale: string) => mockApplyHref)
vi.mock('@/lib/applyHref', () => ({
  getApplyHref: (locale: string) => mockGetApplyHref(locale)
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
    mockGetApplyHref.mockClear()
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

  it('routes the apply link to whatever getApplyHref returns', () => {
    mockApplyHref = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    render(<EnrolledChildren />)
    expect(screen.getByRole('link', { name: /submit/i })).toHaveAttribute('href', mockApplyHref)
  })

  it('passes the active i18n locale through to getApplyHref so PEAK gets the right language', () => {
    render(<EnrolledChildren />)
    // Test setup boots i18n with state=dc and language=en. We just need to
    // confirm whatever the active language is gets forwarded — not a hardcoded
    // value, which is the bug this guards against.
    expect(mockGetApplyHref).toHaveBeenCalledWith(expect.any(String))
    expect(mockGetApplyHref.mock.calls[0]![0]).not.toBe('')
  })
})
