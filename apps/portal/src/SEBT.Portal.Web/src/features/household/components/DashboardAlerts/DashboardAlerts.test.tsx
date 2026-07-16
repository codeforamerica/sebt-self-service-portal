import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import enDcDashboard from '@/content/locales/en/dc/dashboard.json'

import { DashboardAlerts } from './DashboardAlerts'

const mockAddress = {
  streetAddress1: '123 Main St',
  streetAddress2: 'Apt 4B',
  city: 'Washington',
  state: 'DC',
  postalCode: '20001'
}

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
    expect(screen.getByText('Your mailing address has been updated')).toBeInTheDocument()
  })

  it('address success alert shows only the alertAddressUpdated copy, no hardcoded body', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    render(<DashboardAlerts />)

    expect(screen.getByText('Your mailing address has been updated')).toBeInTheDocument()
    expect(screen.queryByText('Your address update has been recorded.')).not.toBeInTheDocument()
  })

  it('renders the contact-preferences success alert when contactUpdated param is present', () => {
    mockSearchParams = new URLSearchParams('contactUpdated=true')
    render(<DashboardAlerts />)

    expect(screen.getByText('Your contact preferences have been updated')).toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/dashboard', { scroll: false })
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

  it('renders card replaced alert with the design body and the household address', () => {
    mockHouseholdData = {
      data: { addressOnFile: mockAddress },
      isLoading: false,
      isError: false
    }
    mockSearchParams = new URLSearchParams('flash=card_replaced')
    render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText(enDcDashboard.alertAddressBody)).toBeInTheDocument()
    expect(screen.getByText('123 Main St')).toBeInTheDocument()
    expect(screen.getByText('Washington, DC 20001')).toBeInTheDocument()
  })

  it('renders the card replaced alert without an address block when none is on file', () => {
    mockSearchParams = new URLSearchParams('flash=card_replaced')
    render(<DashboardAlerts />)

    expect(screen.getByText(enDcDashboard.alertAddressBody)).toBeInTheDocument()
    expect(screen.queryByText('123 Main St')).not.toBeInTheDocument()
  })

  it('renders the address-update-failed warning as a single sentence with no fallback body', () => {
    mockSearchParams = new URLSearchParams('addressUpdateFailed=true')
    render(<DashboardAlerts />)

    expect(
      screen.getByText('There was an issue updating your mailing address. Please try again later.')
    ).toBeInTheDocument()
    // The non-design help-desk fallback body must not appear.
    expect(screen.queryByText(/contact the Summer EBT Help Desk/i)).not.toBeInTheDocument()
  })

  it('renders the contact-update-failed warning exactly once (no duplicate heading + body)', () => {
    mockSearchParams = new URLSearchParams('contactUpdateFailed=true')
    render(<DashboardAlerts />)

    expect(
      screen.getAllByText(
        'There was an issue updating your contact preferences. Please try again later.'
      )
    ).toHaveLength(1)
  })

  describe('address-verification ("Is your address correct?") alert', () => {
    it('renders the heading, body, address, and both actions', () => {
      mockHouseholdData = {
        data: { addressOnFile: mockAddress },
        isLoading: false,
        isError: false
      }
      mockSearchParams = new URLSearchParams('addressVerification=true')
      render(<DashboardAlerts />)

      expect(screen.getByText('Is your address correct?')).toBeInTheDocument()
      expect(screen.getByText(/please check your preferred mailing address/i)).toBeInTheDocument()
      expect(screen.getByText('123 Main St')).toBeInTheDocument()
      expect(screen.getByText('Washington, DC 20001')).toBeInTheDocument()
      expect(screen.getByText('Yes, this is my address')).toBeInTheDocument()

      const changeLink = screen.getByText('No, change my mailing address')
      expect(changeLink).toHaveAttribute('href', '/profile/address')
    })

    it('dismisses the alert when "Yes, this is my address" is clicked', () => {
      mockSearchParams = new URLSearchParams('addressVerification=true')
      render(<DashboardAlerts />)

      expect(screen.getByRole('alert')).toBeInTheDocument()

      fireEvent.click(screen.getByText('Yes, this is my address'))

      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    })

    it('renders without an address block when none is on file', () => {
      mockSearchParams = new URLSearchParams('addressVerification=true')
      render(<DashboardAlerts />)

      expect(screen.getByText('Is your address correct?')).toBeInTheDocument()
      expect(screen.queryByText('123 Main St')).not.toBeInTheDocument()
    })
  })
})
