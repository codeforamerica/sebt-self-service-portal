import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { Application, HouseholdData } from '../../api'

import { HouseholdSummary } from './HouseholdSummary'

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
  addressOnFile: {
    streetAddress1: '123 Main Street',
    streetAddress2: 'Apt 4B',
    city: 'Washington',
    state: 'DC',
    postalCode: '20001'
  }
}

describe('HouseholdSummary', () => {
  it('renders status heading', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableHeadingStatus → "Status"
    expect(screen.getByText('Status')).toBeInTheDocument()
  })

  it('renders enrolled status for approved application', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableStatusEnrolled → "Enrolled"
    const statusText = screen.getByText('Enrolled')
    expect(statusText).toHaveClass('text-bold')
    expect(statusText).toHaveClass('text-green')
  })

  it('renders enrolled status description for approved application', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableStatusEnrolledDescription
    expect(
      screen.getByText(/Your children are enrolled because we have enough information/)
    ).toBeInTheDocument()
  })

  it('renders in-progress status for pending application', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    const pendingData: HouseholdData = { ...mockData, applications: [pendingApp] }

    render(<HouseholdSummary data={pendingData} />)

    // i18n key: profileTableStatusApplicationIn-progress → "Application in-progress"
    const statusText = screen.getByText('Application in-progress')
    expect(statusText).toHaveClass('text-gold')
  })

  it('does not render enrolled description for non-approved status', () => {
    const pendingApp: Application = { ...mockApplication, applicationStatus: 'Pending' }
    const pendingData: HouseholdData = { ...mockData, applications: [pendingApp] }

    render(<HouseholdSummary data={pendingData} />)

    expect(
      screen.queryByText(/Your children are enrolled because we have enough information/)
    ).not.toBeInTheDocument()
  })

  it('renders denied status for denied application', () => {
    const deniedApp: Application = { ...mockApplication, applicationStatus: 'Denied' }
    const deniedData: HouseholdData = { ...mockData, applications: [deniedApp] }

    render(<HouseholdSummary data={deniedData} />)

    // i18n key: profileTableStatusApplicationDenied → "Application denied"
    const statusText = screen.getByText('Application denied')
    expect(statusText).toHaveClass('text-red')
  })

  it('renders mailing address when provided', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableHeadingMailingAddress → "Your mailing address"
    expect(screen.getByText('Your mailing address')).toBeInTheDocument()
    expect(screen.getByText(/123 Main Street/)).toBeInTheDocument()
  })

  it('renders change mailing address link', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableActionChangeAddress → "Change my mailing address"
    const link = screen.getByRole('link', { name: 'Change my mailing address' })
    expect(link).toHaveAttribute('href', '/address')
  })

  it('hides mailing address when not provided', () => {
    const dataWithoutAddress: HouseholdData = { ...mockData, addressOnFile: null }
    render(<HouseholdSummary data={dataWithoutAddress} />)

    expect(screen.queryByText('Your mailing address')).not.toBeInTheDocument()
    expect(screen.queryByText(/123 Main Street/)).not.toBeInTheDocument()
  })

  it('renders preferred contact with email', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableHeadingPreferredContact → "Your preferred contact"
    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.getByText(/test@example.com/)).toBeInTheDocument()
  })

  it('renders change contact information link', () => {
    render(<HouseholdSummary data={mockData} />)

    // i18n key: profileTableActionChangeContact → "Change my contact information"
    const link = screen.getByRole('link', { name: 'Change my contact information' })
    expect(link).toHaveAttribute('href', '/contact')
  })

  it('renders preferred contact with phone when provided', () => {
    render(<HouseholdSummary data={mockData} />)

    expect(screen.getByText(/\(303\) 555-0100/)).toBeInTheDocument()
  })

  it('renders preferred contact without phone when not provided', () => {
    const dataWithoutPhone: HouseholdData = { ...mockData, phone: null }
    render(<HouseholdSummary data={dataWithoutPhone} />)

    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.getByText(/test@example.com/)).toBeInTheDocument()
    expect(screen.queryByText(/\(303\) 555-0100/)).not.toBeInTheDocument()
  })

  it('renders preferred contact with only phone when email not provided', () => {
    const dataWithoutEmail: HouseholdData = { ...mockData, email: null }
    render(<HouseholdSummary data={dataWithoutEmail} />)

    expect(screen.getByText('Your preferred contact')).toBeInTheDocument()
    expect(screen.getByText(/\(303\) 555-0100/)).toBeInTheDocument()
    expect(screen.queryByText(/test@example.com/)).not.toBeInTheDocument()
  })

  it('hides contact section when neither email nor phone provided', () => {
    const dataWithoutContact: HouseholdData = { ...mockData, email: null, phone: null }
    render(<HouseholdSummary data={dataWithoutContact} />)

    expect(screen.queryByText('Your preferred contact')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Change my contact information' })
    ).not.toBeInTheDocument()
  })
})
