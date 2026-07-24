import { i18n } from '@sebt/design-system/client'
import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import type { Application, HouseholdData } from '../../api'

import { ApplicationsSection } from './ApplicationsSection'

const CASE_NUMBER_LABEL_KEY = 'applicationsTableHeadingNumber'

// The test i18n instance loads DC resources, and DC marks the case-number label `!N/A!`
// in dc.csv, so the key is absent. States that do author it (CO) render the block.
function authorCaseNumberLabel() {
  i18n.addResource('en', 'dashboard', CASE_NUMBER_LABEL_KEY, 'Case number')
}

const mockApplication: Application = {
  applicationNumber: 'APP-2026-001',
  caseNumber: 'CASE-DC-2026-001',
  applicationStatus: 'Approved',
  applicationDate: '2026-01-15T00:00:00Z',
  benefitIssueDate: '2026-01-08T00:00:00Z',
  benefitExpirationDate: '2026-03-19T00:00:00Z',
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

// formatDate is stubbed to a deterministic sentinel so date assertions don't
// depend on locale/Intl behavior — we only verify it's wired to applicationDate.
vi.mock('../../api', () => ({
  useRequiredHouseholdData: () => mockReturnData,
  formatDate: (isoDate: string) => `formatted:${isoDate}`
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

// DC omits this label, so no test may inherit it. Cleared before as well as after each test:
// clearing only afterwards would let an earlier test, or a stray injection at import time,
// silently decide the outcome of "omits the case number when the state does not author a label".
function clearCaseNumberLabel() {
  const bundle = i18n.getResourceBundle('en', 'dashboard') as Record<string, string> | undefined
  if (bundle) {
    delete bundle.applicationsTableHeadingNumber
  }
}

describe('ApplicationsSection', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
    clearCaseNumberLabel()
  })

  afterEach(clearCaseNumberLabel)

  it('renders section heading', () => {
    renderWithFlags()

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
  })

  it('omits the case number when the state does not author a label', () => {
    renderWithFlags()

    expect(screen.queryByText(CASE_NUMBER_LABEL_KEY)).not.toBeInTheDocument()
    expect(screen.queryByText('CASE-DC-2026-001')).not.toBeInTheDocument()
  })

  it('renders the case number when the state authors a label', () => {
    authorCaseNumberLabel()

    renderWithFlags()

    expect(screen.getByText('Case number')).toBeInTheDocument()
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
    expect(statusText).toHaveClass('text-green')
  })

  it('renders a safe default label for an unmapped status', () => {
    const unknownApp: Application = { ...mockApplication, applicationStatus: 'Unknown' }
    mockReturnData = { ...defaultMockData, applications: [unknownApp] }

    renderWithFlags()

    expect(screen.getByText('Status unavailable')).toBeInTheDocument()
    expect(screen.queryByText('Unknown')).not.toBeInTheDocument()
  })

  it('renders an unmapped status with neutral rather than positive text', () => {
    const unknownApp: Application = { ...mockApplication, applicationStatus: 'Unknown' }
    mockReturnData = { ...defaultMockData, applications: [unknownApp] }

    renderWithFlags()

    const statusText = screen.getByText('Status unavailable')
    expect(statusText).not.toHaveClass('text-green')
    expect(statusText).toHaveClass('text-base-dark')
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

    // Children are rendered unconditionally, unlike the flag- and label-gated case number.
    expect(screen.getByText('Sophia Martinez, James Martinez')).toBeInTheDocument()
    expect(screen.getByText('Emily Brown')).toBeInTheDocument()
  })

  it('hides case number when show_case_number flag is off', () => {
    authorCaseNumberLabel()

    renderWithFlags({
      flags: { ...TEST_FEATURE_FLAGS, show_case_number: false },
      isLoading: false,
      isError: false
    })

    expect(screen.queryByText('CASE-DC-2026-001')).not.toBeInTheDocument()
  })

  it('renders application date when show_application_date flag is on', () => {
    renderWithFlags()

    expect(screen.getByText('formatted:2026-01-15T00:00:00Z')).toBeInTheDocument()
  })

  it('hides application date when show_application_date flag is off', () => {
    renderWithFlags({
      flags: { ...TEST_FEATURE_FLAGS, show_application_date: false },
      isLoading: false,
      isError: false
    })

    expect(screen.queryByText('formatted:2026-01-15T00:00:00Z')).not.toBeInTheDocument()
  })

  it('hides application date when show_application_date is not enabled', () => {
    // CO does not enable this flag, so it is absent from GET /api/features and
    // useFeatureFlag falls back to its `?? false` default — the date must stay
    // hidden even when an application carries one.
    const flagsWithoutApplicationDate = Object.fromEntries(
      Object.entries(TEST_FEATURE_FLAGS).filter(([name]) => name !== 'show_application_date')
    )

    renderWithFlags({
      flags: flagsWithoutApplicationDate,
      isLoading: false,
      isError: false
    })

    expect(screen.queryByText('formatted:2026-01-15T00:00:00Z')).not.toBeInTheDocument()
  })

  it('hides application date when applicationDate is absent', () => {
    const appWithoutDate: Application = { ...mockApplication, applicationDate: undefined }
    mockReturnData = {
      ...defaultMockData,
      applications: [appWithoutDate]
    }

    renderWithFlags()

    expect(screen.queryByText(/^formatted:/)).not.toBeInTheDocument()
  })
})
