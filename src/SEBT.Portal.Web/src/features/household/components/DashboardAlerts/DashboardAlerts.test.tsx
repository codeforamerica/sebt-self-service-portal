import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import enDcDashboard from '@/content/locales/en/dc/dashboard.json'

import { DashboardAlerts } from './DashboardAlerts'

let mockHouseholdData: { data: unknown; isLoading: boolean; isError: boolean } = {
  data: null,
  isLoading: false,
  isError: false
}

vi.mock('@/features/household', () => ({
  useHouseholdData: () => mockHouseholdData
}))

const mockReplace = vi.fn()
let mockSearchParams = new URLSearchParams()

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
  useRouter: () => ({
    replace: mockReplace
  }),
  usePathname: () => '/dashboard'
}))

describe('DashboardAlerts', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockSearchParams = new URLSearchParams()
    mockHouseholdData = { data: null, isLoading: false, isError: false }
  })

  it('renders nothing when no alert params are present', () => {
    const { container } = render(<DashboardAlerts />)

    expect(container.querySelector('.usa-alert')).not.toBeInTheDocument()
  })

  it('renders address success alert when addressUpdated param is present', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    // Heading resolves from the `alertAddressUpdated` key, not a missing one.
    expect(screen.getByText('Your mailing address has been updated')).toBeInTheDocument()
  })

  it('address success alert shows only the alertAddressUpdated copy, no hardcoded body', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    render(<DashboardAlerts />)

    expect(screen.getByText('Your mailing address has been updated')).toBeInTheDocument()
    // There is no separate body-level content key, so the alert must not fall
    // back to a hardcoded body string.
    expect(screen.queryByText('Your address update has been recorded.')).not.toBeInTheDocument()
  })

  it('renders card request alert when both addressUpdated and cardsRequested params are present', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true&cardsRequested=true')
    render(<DashboardAlerts />)

    const alerts = screen.getAllByRole('alert')
    expect(alerts.length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/card replacement recorded/i)).toBeInTheDocument()
  })

  it('cleans URL params after displaying alerts', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    render(<DashboardAlerts />)

    expect(mockReplace).toHaveBeenCalledWith('/dashboard', { scroll: false })
  })

  it('does not clean params when no alert params are present', () => {
    mockSearchParams = new URLSearchParams()
    render(<DashboardAlerts />)

    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('alert persists after URL params are cleaned', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    const { rerender } = render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/dashboard', { scroll: false })

    // Simulate the re-render triggered by useSearchParams reacting to cleaned URL
    mockSearchParams = new URLSearchParams()
    rerender(<DashboardAlerts />)

    // Alert should still be visible even though params are gone
    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Your mailing address has been updated')).toBeInTheDocument()
  })

  it('renders card replaced alert when flash=card_replaced param is present', () => {
    mockSearchParams = new URLSearchParams('flash=card_replaced')
    render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText(/replacement card request has been recorded/i)).toBeInTheDocument()
  })

  it('card-replaced with-address body resolves dashboard.alertAddressBody, not a hardcoded fallback', () => {
    mockHouseholdData = {
      data: { addressOnFile: '123 Main St' },
      isLoading: false,
      isError: false
    }
    mockSearchParams = new URLSearchParams('flash=card_replaced')
    render(<DashboardAlerts />)

    expect(screen.getByText(enDcDashboard.alertAddressBody)).toBeInTheDocument()
  })

  it('renders the address-verification warning with a resolved heading and body', () => {
    mockSearchParams = new URLSearchParams('addressVerification=true')
    render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Is your address correct?')).toBeInTheDocument()
    // Body resolves from `alertCheckAddressBody`, the semantic pair of the
    // `alertCheckAddressTitle` heading.
    expect(screen.getByText(/please check your preferred mailing address/i)).toBeInTheDocument()
  })

  it('renders the address-update-failed warning heading from a resolved key', () => {
    mockSearchParams = new URLSearchParams('addressUpdateFailed=true')
    render(<DashboardAlerts />)

    expect(
      screen.getByText('There was an issue updating your mailing address. Please try again later.')
    ).toBeInTheDocument()
  })

  it('renders the contact-update-failed warning heading from a resolved key', () => {
    mockSearchParams = new URLSearchParams('contactUpdateFailed=true')
    render(<DashboardAlerts />)

    // alertContactUpdateError currently renders as both heading and body in DashboardAlerts — pre-existing, out of scope here
    expect(
      screen.getAllByText(
        'There was an issue updating your contact preferences. Please try again later.'
      ).length
    ).toBeGreaterThan(0)
  })

  it('combined alert persists after URL params are cleaned', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true&cardsRequested=true')
    const { rerender } = render(<DashboardAlerts />)

    expect(screen.getByText(/card replacement recorded/i)).toBeInTheDocument()

    // Simulate URL cleanup re-render
    mockSearchParams = new URLSearchParams()
    rerender(<DashboardAlerts />)

    expect(screen.getByText(/card replacement recorded/i)).toBeInTheDocument()
  })
})
