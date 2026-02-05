import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

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

const mockData: HouseholdData = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  applications: [mockApplication],
  addressOnFile: null
}

describe('EnrolledChildren', () => {
  it('renders section heading', () => {
    render(<EnrolledChildren data={mockData} />)

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('renders a child card for each child', () => {
    render(<EnrolledChildren data={mockData} />)

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('James Martinez')).toBeInTheDocument()
  })

  it('expands only the first child by default', () => {
    render(<EnrolledChildren data={mockData} />)

    const buttons = screen.getAllByRole('button')
    expect(buttons[0]).toHaveAttribute('aria-expanded', 'true')
    expect(buttons[1]).toHaveAttribute('aria-expanded', 'false')
  })

  it('renders accordion with bordered variant', () => {
    const { container } = render(<EnrolledChildren data={mockData} />)

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
    const multiAppData: HouseholdData = {
      ...mockData,
      applications: [firstApp, secondApp]
    }

    render(<EnrolledChildren data={multiAppData} />)

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })
})
