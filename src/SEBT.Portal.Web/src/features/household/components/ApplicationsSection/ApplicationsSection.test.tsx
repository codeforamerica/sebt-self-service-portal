import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

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
    { firstName: 'Sophia', lastName: 'Martinez' },
    { firstName: 'James', lastName: 'Martinez' }
  ],
  childrenOnApplication: 2
}

const defaultMockData: HouseholdData = {
  email: 'test@example.com',
  phone: '3035550100',
  summerEbtCases: [],
  applications: [mockApplication],
  addressOnFile: null,
  coLoadedCohort: 'NonCoLoaded'
}

let mockReturnData: HouseholdData

vi.mock('../../api', () => ({
  useRequiredHouseholdData: () => mockReturnData
}))

const defaultFlags: FeatureFlagsContextValue = {
  flags: TEST_FEATURE_FLAGS,
  isLoading: false,
  isError: false
}

function renderWithFlags(flags: FeatureFlagsContextValue = defaultFlags) {
  return render(
    <FeatureFlagsContext.Provider value={flags}>
      <ApplicationsSection />
    </FeatureFlagsContext.Provider>
  )
}

describe('ApplicationsSection', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
  })

  it('renders section heading', () => {
    renderWithFlags()

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('renders case number', () => {
    renderWithFlags()

    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
  })

  it('renders children names', () => {
    renderWithFlags()

    expect(screen.getByText('Sophia Martinez, James Martinez')).toBeInTheDocument()
  })

  it('renders application status with green text for approved', () => {
    renderWithFlags()

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

    renderWithFlags()

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

    renderWithFlags()

    const statusText = screen.getByText('Pending')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-gold')
  })

  it('renders nothing when no applications', () => {
    mockReturnData = {
      ...defaultMockData,
      applications: []
    }

    const { container } = renderWithFlags()

    expect(container).toBeEmptyDOMElement()
  })

  it('renders multiple application cards', () => {
    const secondApp: Application = {
      ...mockApplication,
      applicationNumber: 'APP-2026-002',
      caseNumber: 'CASE-DC-2026-002',
      applicationStatus: 'Pending',
      children: [{ firstName: 'Emily', lastName: 'Brown' }],
      childrenOnApplication: 1
    }
    mockReturnData = {
      ...defaultMockData,
      applications: [mockApplication, secondApp]
    }

    renderWithFlags()

    // Verify both case numbers are shown
    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
    expect(screen.getByText('CASE-DC-2026-002')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })

  it('hides case number when show_case_number flag is off', () => {
    renderWithFlags({
      flags: { ...TEST_FEATURE_FLAGS, show_case_number: false },
      isLoading: false,
      isError: false
    })

    expect(screen.queryByText('CASE-DC-2026-001')).not.toBeInTheDocument()
  })
})
