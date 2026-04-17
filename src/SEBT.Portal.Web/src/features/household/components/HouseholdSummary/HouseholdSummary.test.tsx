import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Application, HouseholdData, SummerEbtCase } from '../../api'

import { HouseholdSummary } from './HouseholdSummary'

const mockCase: SummerEbtCase = {
  summerEBTCaseID: 'SEBT-001',
  childFirstName: 'Sophia',
  childLastName: 'Martinez',
  householdType: 'OSSE',
  eligibilityType: 'NSLP',
  issuanceType: 'SummerEbt',
  allowAddressChange: true,
  allowCardReplacement: true
}

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
  summerEbtCases: [mockCase],
  applications: [mockApplication],
  addressOnFile: {
    streetAddress1: '1350 Pennsylvania Ave NW',
    streetAddress2: 'Suite 400',
    city: 'Washington',
    state: 'DC',
    postalCode: '20004'
  }
}

let mockReturnData: HouseholdData

vi.mock('../../api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api')>()),
  useRequiredHouseholdData: () => mockReturnData
}))

describe('HouseholdSummary', () => {
  beforeEach(() => {
    mockReturnData = defaultMockData
  })

  it('renders status heading', () => {
    render(<HouseholdSummary />)
    expect(screen.getByText('Status')).toBeInTheDocument()
  })

  it('renders enrolled status when cases exist', () => {
    render(<HouseholdSummary />)
    const statusText = screen.getByText('Enrolled')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-green')
  })

  it('renders enrolled status description when cases exist', () => {
    render(<HouseholdSummary />)
    expect(
      screen.getByText(/Your children are enrolled because we have enough information/)
    ).toBeInTheDocument()
  })

  it('renders combined status when cases exist with pending application', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    mockReturnData = { ...defaultMockData, applications: [pendingApp] }
    render(<HouseholdSummary />)
    expect(screen.getByText('Enrolled')).toBeInTheDocument()
    expect(screen.getByText('Application in-progress')).toBeInTheDocument()
  })

  it('renders in-progress status for pending application when no cases', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    mockReturnData = { ...defaultMockData, summerEbtCases: [], applications: [pendingApp] }
    render(<HouseholdSummary />)
    const statusText = screen.getByText('Application in-progress')
    expect(statusText).toHaveClass('text-gold')
  })

  it('does not render enrolled description when no cases and application pending', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    mockReturnData = { ...defaultMockData, summerEbtCases: [], applications: [pendingApp] }
    render(<HouseholdSummary />)
    expect(
      screen.queryByText(/Your children are enrolled because we have enough information/)
    ).not.toBeInTheDocument()
  })

  it('renders denied status for denied application when no cases', () => {
    const deniedApp: Application = { ...mockApplication, applicationStatus: 'Denied' }
    mockReturnData = { ...defaultMockData, summerEbtCases: [], applications: [deniedApp] }
    render(<HouseholdSummary />)
    const statusText = screen.getByText('Application denied')
    expect(statusText).toHaveClass('text-red')
  })

  it('does not show secondary status when all applications are approved', () => {
    render(<HouseholdSummary />)
    expect(screen.getByText('Enrolled')).toBeInTheDocument()
    expect(screen.queryByText('Application denied')).not.toBeInTheDocument()
    expect(screen.queryByText('Application in-progress')).not.toBeInTheDocument()
  })

  it('renders mailing address when provided', () => {
    render(<HouseholdSummary />)
    expect(screen.getByText('Your mailing address')).toBeInTheDocument()
    expect(screen.getByText(/1350 Pennsylvania Ave NW/)).toBeInTheDocument()
  })

  it('renders change mailing address link', () => {
    render(<HouseholdSummary />)
    const link = screen.getByRole('link', { name: 'Change my mailing address' })
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('exposes data-analytics-cta on the change mailing address link', () => {
    render(<HouseholdSummary />)
    const link = screen.getByRole('link', { name: 'Change my mailing address' })
    expect(link).toHaveAttribute('data-analytics-cta', 'update_address_cta')
  })

  it('shows info link (not action link) when allowedActions.canUpdateAddress is false', () => {
    mockReturnData = {
      ...defaultMockData,
      allowedActions: {
        canUpdateAddress: false,
        addressUpdateDeniedMessageKey: 'actionNavigationSelfServiceUnavailable',
        canRequestReplacementCard: false,
        cardReplacementDeniedMessageKey: null
      }
    }
    render(<HouseholdSummary />)
    expect(
      screen.queryByRole('link', { name: 'Change my mailing address' })
    ).not.toBeInTheDocument()
    const infoLink = screen.getByRole('link', { name: /how to change your mailing address/i })
    expect(infoLink).toHaveAttribute('href', '/profile/address/info')
    expect(infoLink).toHaveAttribute('data-analytics-cta', 'update_address_info_cta')
  })

  it('shows change mailing address link when allowedActions.canUpdateAddress is true', () => {
    mockReturnData = {
      ...defaultMockData,
      allowedActions: {
        canUpdateAddress: true,
        addressUpdateDeniedMessageKey: null,
        canRequestReplacementCard: true,
        cardReplacementDeniedMessageKey: null
      }
    }
    render(<HouseholdSummary />)
    expect(screen.getByRole('link', { name: 'Change my mailing address' })).toBeInTheDocument()
  })

  it('exposes data-analytics-cta on the change contact preferences link', () => {
    render(<HouseholdSummary />)
    const link = screen.getByRole('link', { name: 'Change my contact preferences' })
    expect(link).toHaveAttribute('data-analytics-cta', 'update_contact_cta')
  })

  it('hides mailing address when not provided', () => {
    mockReturnData = { ...defaultMockData, addressOnFile: null }
    render(<HouseholdSummary />)
    expect(screen.queryByText('Your mailing address')).not.toBeInTheDocument()
    expect(screen.queryByText(/1350 Pennsylvania Ave NW/)).not.toBeInTheDocument()
  })

  it('renders preferred contact with email', () => {
    render(<HouseholdSummary />)
    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.getByText(/test@example.com/)).toBeInTheDocument()
  })

  it('renders change contact information link', () => {
    render(<HouseholdSummary />)
    const link = screen.getByRole('link', { name: 'Change my contact preferences' })
    expect(link).toHaveAttribute('href', '/contact')
  })

  it('renders preferred contact with phone when provided', () => {
    render(<HouseholdSummary />)
    expect(screen.getByText(/303-555-0100/)).toBeInTheDocument()
  })

  it('renders preferred contact without phone when not provided', () => {
    mockReturnData = { ...defaultMockData, phone: null }
    render(<HouseholdSummary />)
    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.queryByText(/303-555-0100/)).not.toBeInTheDocument()
  })

  it('renders preferred contact with only phone when email not provided', () => {
    mockReturnData = { ...defaultMockData, email: null }
    render(<HouseholdSummary />)
    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.getByText(/303-555-0100/)).toBeInTheDocument()
  })

  it('hides contact section when neither email nor phone provided', () => {
    mockReturnData = { ...defaultMockData, email: null, phone: null }
    render(<HouseholdSummary />)
    expect(screen.queryByText('Your preferred contact')).not.toBeInTheDocument()
  })
})
