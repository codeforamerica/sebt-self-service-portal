import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { AllowedActions } from '../../api'
import { ActionButtons } from './ActionButtons'

vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: vi.fn().mockReturnValue('dc')
  }
})

const { getState } = await import('@sebt/design-system')
const mockGetState = vi.mocked(getState)

const allAllowed: AllowedActions = {
  canUpdateAddress: true,
  canRequestReplacementCard: true
}

const allDenied: AllowedActions = {
  canUpdateAddress: false,
  canRequestReplacementCard: false,
  addressUpdateDeniedMessageKey: 'actionNavigationSelfServiceUnavailable',
  cardReplacementDeniedMessageKey: 'actionNavigationSelfServiceUnavailable'
}

describe('ActionButtons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
  })

  it('renders navigation element with aria-label', () => {
    render(<ActionButtons />)

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons when all actions allowed', () => {
    render(<ActionButtons allowedActions={allAllowed} />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '/cards')
  })

  it('renders request replacement cards button when allowed', () => {
    render(<ActionButtons allowedActions={allAllowed} />)

    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button when allowed', () => {
    render(<ActionButtons allowedActions={allAllowed} />)

    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders check applications button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '/applications')
  })

  it('renders "I want to" heading', () => {
    render(<ActionButtons />)

    expect(screen.getByText('I want to')).toBeInTheDocument()
  })

  it('renders pill-shaped buttons', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      expect(link).toHaveClass('radius-pill')
    })
  })

  // ── Self-service eligibility (driven by allowedActions) ──

  it('hides gated CTAs when both actions denied', () => {
    render(<ActionButtons allowedActions={allDenied} />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
  })

  it('hides only address CTA when address update denied', () => {
    render(
      <ActionButtons
        allowedActions={{ canUpdateAddress: false, canRequestReplacementCard: true }}
      />
    )

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(3)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.getByText('Request new cards')).toBeInTheDocument()
  })

  it('shows info alert when any action is denied', () => {
    render(<ActionButtons allowedActions={allDenied} />)

    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('does not show info alert when all actions allowed', () => {
    render(<ActionButtons allowedActions={allAllowed} />)

    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows all CTAs when no allowedActions provided', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  describe('DC state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })

    it('renders buttons with secondary background and ink text', () => {
      render(<ActionButtons />)

      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-secondary')
        expect(link).toHaveClass('text-ink')
      })
    })
  })

  describe('CO state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('co')
    })

    it('renders buttons with primary background and white text', () => {
      render(<ActionButtons />)

      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-primary')
        expect(link).toHaveClass('text-white')
      })
    })
  })
})
