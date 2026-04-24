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

const allowAll: AllowedActions = {
  canUpdateAddress: true,
  canRequestReplacementCard: true,
  addressUpdateDeniedMessageKey: null,
  cardReplacementDeniedMessageKey: null
}

const denyAll: AllowedActions = {
  canUpdateAddress: false,
  canRequestReplacementCard: false,
  addressUpdateDeniedMessageKey: 'address_update.not_allowed',
  cardReplacementDeniedMessageKey: 'card_replacement.not_allowed'
}

describe('ActionButtons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
  })

  it('renders navigation element with aria-label', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons when all actions are allowed', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '#enrolled-children-heading')
  })

  it('renders request replacement cards button', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders check applications button', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '#applications-heading')
  })

  it('exposes data-analytics-cta on each action for cta_click tracking', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    expect(screen.getByText('Change my mailing address').closest('a')).toHaveAttribute(
      'data-analytics-cta',
      'update_address_cta'
    )
    expect(screen.getByText('Request new cards').closest('a')).toHaveAttribute(
      'data-analytics-cta',
      'replacement_card_cta'
    )
    expect(screen.getByText('Check existing cards').closest('a')).toHaveAttribute(
      'data-analytics-cta',
      'check_cards_cta'
    )
    expect(screen.getByText('Check existing applications').closest('a')).toHaveAttribute(
      'data-analytics-cta',
      'check_applications_cta'
    )
  })

  it('renders "I want to" heading', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    expect(screen.getByText('I want to')).toBeInTheDocument()
  })

  it('renders pill-shaped buttons', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      expect(link).toHaveClass('radius-pill')
    })
  })

  it('hides address update CTA when canUpdateAddress is false', () => {
    render(<ActionButtons allowedActions={{ ...allowAll, canUpdateAddress: false }} />)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.getByText('Request new cards')).toBeInTheDocument()
  })

  it('hides card replacement CTA when canRequestReplacementCard is false', () => {
    render(<ActionButtons allowedActions={{ ...allowAll, canRequestReplacementCard: false }} />)
    expect(screen.queryByText('Request new cards')).toBeNull()
    expect(screen.getByText('Change my mailing address')).toBeInTheDocument()
  })

  it('hides all gated CTAs when all self-service actions are denied', () => {
    render(<ActionButtons allowedActions={denyAll} />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
  })

  it('shows info alert when at least one self-service action is denied', () => {
    render(<ActionButtons allowedActions={denyAll} />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('does not show info alert when all self-service actions are allowed', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows all CTAs when allowedActions is not provided (backward compatible)', () => {
    render(<ActionButtons />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
    expect(screen.queryByRole('status')).toBeNull()
  })

  describe('DC state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })
    it('renders buttons with secondary background and ink text', () => {
      render(<ActionButtons allowedActions={allowAll} />)
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
      render(<ActionButtons allowedActions={allowAll} />)
      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-primary')
        expect(link).toHaveClass('text-white')
      })
    })
  })
})
