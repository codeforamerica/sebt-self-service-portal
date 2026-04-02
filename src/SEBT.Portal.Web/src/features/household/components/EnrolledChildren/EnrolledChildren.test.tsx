import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Application, HouseholdData } from '../../api'

import { EnrolledChildren } from './EnrolledChildren'

const mockApplication: Application = {
  applicationNumber: 'APP-2026-001',
  caseNumber: 'CASE-DC-2026-001',
  applicationStatus: 'Approved',
  benefitIssueDate: '2026-01-08T00:00:00Z',
  benefitExpirationDate: '2026-03-19T00:00:00Z',
  last4DigitsOfCard: '1234',
  cardStatus: 'Active',
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardActivatedAt: '2026-01-08T00:00:00Z',
  cardDeactivatedAt: null,
  children: [
    { caseNumber: 456001, firstName: 'Sophia', lastName: 'Martinez' },
    { caseNumber: 456002, firstName: 'James', lastName: 'Martinez' }
  ],
  childrenOnApplication: 2
}

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  summerEbtCases: [],
  applications: [mockApplication],
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

  it('renders a child card for each child', () => {
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

  it('renders children from multiple applications', () => {
    const firstApp: Application = {
      ...mockApplication,
      children: [{ caseNumber: 456001, firstName: 'Sophia', lastName: 'Martinez' }],
      childrenOnApplication: 1
    }
    const secondApp: Application = {
      ...mockApplication,
      applicationNumber: 'APP-2026-002',
      children: [{ caseNumber: 456003, firstName: 'Emily', lastName: 'Brown' }],
      childrenOnApplication: 1
    }
    mockReturnData = {
      ...defaultMockData,
      applications: [firstApp, secondApp]
    }

    render(<EnrolledChildren />)

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })
})
