import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

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
  children: [{ firstName: 'Sophia', lastName: 'Martinez' }],
  childrenOnApplication: 1
}

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '3035550100',
  summerEbtCases: [],
  applications: [mockApplication],
  addressOnFile: null,
  userProfile: {
    firstName: 'Maria',
    middleName: 'L',
    lastName: 'Martinez'
  },
  coLoadedCohort: 'NonCoLoaded'
}

let mockReturnData: HouseholdData

vi.mock('../../api', () => ({
  useRequiredHouseholdData: () => mockReturnData
}))

describe('UserProfileCard', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
  })

  it('renders user initials in avatar', () => {
    render(<UserProfileCard />)

    expect(screen.getByText('MM')).toBeInTheDocument()
  })

  it('renders full name with middle initial', () => {
    render(<UserProfileCard />)

    expect(screen.getByText('Maria L. Martinez')).toBeInTheDocument()
  })

  it('renders full name without middle initial when not provided', () => {
    mockReturnData = {
      ...defaultMockData,
      userProfile: {
        firstName: 'Maria',
        middleName: null,
        lastName: 'Martinez'
      }
    }

    render(<UserProfileCard />)

    expect(screen.getByText('Maria Martinez')).toBeInTheDocument()
  })

  it('renders logout link', () => {
    render(<UserProfileCard />)

    const logoutLink = screen.getByRole('link', { name: /logout|sign out/i })
    expect(logoutLink).toHaveAttribute('href', '/api/auth/logout')
  })

  it('logout link points to /api/auth/logout', () => {
    render(<UserProfileCard />)

    const logoutLink = screen.getByRole('link', { name: /logout|sign out/i })
    expect(logoutLink).toHaveAttribute('href', '/api/auth/logout')
  })

  it('renders nothing when no userProfile', () => {
    mockReturnData = {
      ...defaultMockData,
      userProfile: null
    }

    const { container } = render(<UserProfileCard />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders mononym user with only first name', () => {
    mockReturnData = {
      ...defaultMockData,
      userProfile: {
        firstName: 'Prince',
        middleName: null,
        lastName: null
      }
    }

    render(<UserProfileCard />)

    // Should show just first name
    expect(screen.getByText('Prince')).toBeInTheDocument()
    // Should show single initial
    expect(screen.getByText('P')).toBeInTheDocument()
  })

  it('renders mononym user with first name and middle name', () => {
    mockReturnData = {
      ...defaultMockData,
      userProfile: {
        firstName: 'Madonna',
        middleName: 'L',
        lastName: null
      }
    }

    render(<UserProfileCard />)

    // Should show first name with middle initial
    expect(screen.getByText('Madonna L.')).toBeInTheDocument()
    // Should show single initial (from first name only)
    expect(screen.getByText('M')).toBeInTheDocument()
  })
})
