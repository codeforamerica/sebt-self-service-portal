import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { Application, HouseholdData } from '../../api'

import { ApplicationsSection } from './ApplicationsSection'

const mockApplication: Application = {
  applicationNumber: 'APP-2026-001',
  caseNumber: 'CASE-DC-2026-001',
  applicationStatus: 'Approved',
  benefitIssueDate: '2026-01-08T00:00:00Z',
  benefitExpirationDate: '2026-03-19T00:00:00Z',
  last4DigitsOfCard: '1234',
  cardStatus: 'Active',
  cardRequestedAt: null,
  cardMailedAt: null,
  cardActivatedAt: null,
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

describe('ApplicationsSection', () => {
  it('renders section heading', () => {
    render(<ApplicationsSection data={mockData} />)

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('renders case number', () => {
    render(<ApplicationsSection data={mockData} />)

    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
  })

  it('renders children names', () => {
    render(<ApplicationsSection data={mockData} />)

    expect(screen.getByText('Sophia Martinez, James Martinez')).toBeInTheDocument()
  })

  it('renders application status with green text for approved', () => {
    render(<ApplicationsSection data={mockData} />)

    const statusText = screen.getByText('Approved')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-green')
  })

  it('renders denied status with red text', () => {
    const deniedApp: Application = { ...mockApplication, applicationStatus: 'Denied' }
    const deniedData: HouseholdData = {
      ...mockData,
      applications: [deniedApp]
    }

    render(<ApplicationsSection data={deniedData} />)

    const statusText = screen.getByText('Denied')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-red')
  })

  it('renders pending status with gold text', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    const pendingData: HouseholdData = {
      ...mockData,
      applications: [pendingApp]
    }

    render(<ApplicationsSection data={pendingData} />)

    const statusText = screen.getByText('Pending')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-gold')
  })

  it('renders nothing when no applications', () => {
    const emptyData: HouseholdData = {
      ...mockData,
      applications: []
    }

    const { container } = render(<ApplicationsSection data={emptyData} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders multiple application cards', () => {
    const secondApp: Application = {
      ...mockApplication,
      applicationNumber: 'APP-2026-002',
      caseNumber: 'CASE-DC-2026-002',
      applicationStatus: 'Pending',
      children: [{ caseNumber: 456003, firstName: 'Emily', lastName: 'Brown' }],
      childrenOnApplication: 1
    }
    const multiAppData: HouseholdData = {
      ...mockData,
      applications: [mockApplication, secondApp]
    }

    render(<ApplicationsSection data={multiAppData} />)

    // Verify both case numbers are shown
    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
    expect(screen.getByText('CASE-DC-2026-002')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })
})
