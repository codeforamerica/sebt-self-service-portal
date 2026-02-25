import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

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

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  applications: [mockApplication],
  addressOnFile: null
}

let mockReturnData: HouseholdData

vi.mock('../../api', () => ({
  useRequiredHouseholdData: () => mockReturnData
}))

describe('ApplicationsSection', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
  })

  it('renders section heading', () => {
    render(<ApplicationsSection />)

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('renders case number', () => {
    render(<ApplicationsSection />)

    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
  })

  it('renders children names', () => {
    render(<ApplicationsSection />)

    expect(screen.getByText('Sophia Martinez, James Martinez')).toBeInTheDocument()
  })

  it('renders application status with green text for approved', () => {
    render(<ApplicationsSection />)

    const statusText = screen.getByText('Approved')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-green')
  })

  it('renders denied status with red text', () => {
    const deniedApp: Application = { ...mockApplication, applicationStatus: 'Denied' }
    mockReturnData = {
      ...defaultMockData,
      applications: [deniedApp]
    }

    render(<ApplicationsSection />)

    const statusText = screen.getByText('Denied')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-red')
  })

  it('renders pending status with gold text', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    mockReturnData = {
      ...defaultMockData,
      applications: [pendingApp]
    }

    render(<ApplicationsSection />)

    const statusText = screen.getByText('Pending')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-gold')
  })

  it('renders nothing when no applications', () => {
    mockReturnData = {
      ...defaultMockData,
      applications: []
    }

    const { container } = render(<ApplicationsSection />)

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
    mockReturnData = {
      ...defaultMockData,
      applications: [mockApplication, secondApp]
    }

    render(<ApplicationsSection />)

    // Verify both case numbers are shown
    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
    expect(screen.getByText('CASE-DC-2026-002')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })
})
