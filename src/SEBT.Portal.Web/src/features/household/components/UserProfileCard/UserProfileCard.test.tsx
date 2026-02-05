import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { Application, HouseholdData } from '../../api'

import { UserProfileCard } from './UserProfileCard'

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
  children: [{ caseNumber: 456001, firstName: 'Sophia', lastName: 'Martinez' }],
  childrenOnApplication: 1
}

const mockData: HouseholdData = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  applications: [mockApplication],
  addressOnFile: null,
  userProfile: {
    firstName: 'Maria',
    middleName: 'L',
    lastName: 'Martinez'
  }
}

describe('UserProfileCard', () => {
  it('renders user initials in avatar', () => {
    render(<UserProfileCard data={mockData} />)

    expect(screen.getByText('MM')).toBeInTheDocument()
  })

  it('renders full name with middle initial', () => {
    render(<UserProfileCard data={mockData} />)

    expect(screen.getByText('Maria L. Martinez')).toBeInTheDocument()
  })

  it('renders full name without middle initial when not provided', () => {
    const dataWithoutMiddle: HouseholdData = {
      ...mockData,
      userProfile: {
        firstName: 'Maria',
        middleName: null,
        lastName: 'Martinez'
      }
    }

    render(<UserProfileCard data={dataWithoutMiddle} />)

    expect(screen.getByText('Maria Martinez')).toBeInTheDocument()
  })

  it('renders logout link', () => {
    render(<UserProfileCard data={mockData} />)

    const logoutLink = screen.getByRole('link')
    expect(logoutLink).toHaveAttribute('href', '/logout')
  })

  it('renders nothing when no userProfile', () => {
    const dataWithoutProfile: HouseholdData = {
      ...mockData,
      userProfile: null
    }

    const { container } = render(<UserProfileCard data={dataWithoutProfile} />)

    expect(container).toBeEmptyDOMElement()
  })
})
