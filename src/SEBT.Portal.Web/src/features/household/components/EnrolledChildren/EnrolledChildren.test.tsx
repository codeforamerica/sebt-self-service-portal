import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData, SummerEbtCase } from '../../api'

import { EnrolledChildren } from './EnrolledChildren'

const mockCase1: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-001',
  childFirstName: 'Sophia',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt',
  ebtCardLastFour: '1234',
  ebtCardStatus: 'ACTIVE',
  benefitAvailableDate: '2026-06-01T00:00:00Z',
  benefitExpirationDate: '2026-08-31T00:00:00Z'
}

const mockCase2: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-002',
  childFirstName: 'James',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt'
}

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  summerEbtCases: [mockCase1, mockCase2],
  applications: [],
  addressOnFile: null
}

let mockReturnData: HouseholdData

vi.mock('../../api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api')>()),
  useRequiredHouseholdData: () => mockReturnData
}))

describe('EnrolledChildren', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
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
})
