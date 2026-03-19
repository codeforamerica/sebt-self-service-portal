import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DashboardAlerts } from './DashboardAlerts'

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
  })

  it('renders nothing when no alert params are present', () => {
    const { container } = render(<DashboardAlerts />)

    expect(container.querySelector('.usa-alert')).not.toBeInTheDocument()
  })

  it('renders address success alert when addressUpdated param is present', () => {
    mockSearchParams = new URLSearchParams('addressUpdated=true')
    render(<DashboardAlerts />)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText(/address update recorded/i)).toBeInTheDocument()
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
    expect(screen.getByText(/address update recorded/i)).toBeInTheDocument()
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
