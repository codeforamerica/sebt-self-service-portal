import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { SummerEbtCase } from '../../api'

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

function makeCaseWithIssuance(issuanceType: string): SummerEbtCase {
  return {
    childFirstName: 'Test',
    childLastName: 'Child',
    householdType: 'SEBT',
    eligibilityType: 'NSLP',
    issuanceType: issuanceType as SummerEbtCase['issuanceType']
  }
}

describe('ActionButtons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
  })

  it('renders navigation element with aria-label', () => {
    render(<ActionButtons cases={[]} />)
    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons for SummerEbt cases', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('SummerEbt')]} />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons cases={[]} />)
    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '#enrolled-children-heading')
  })

  it('renders request replacement cards button', () => {
    render(<ActionButtons cases={[]} />)
    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button', () => {
    render(<ActionButtons cases={[]} />)
    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders check applications button', () => {
    render(<ActionButtons cases={[]} />)
    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '#applications-heading')
  })

  it('exposes data-analytics-cta on each action for cta_click tracking', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('SummerEbt')]} />)
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
    render(<ActionButtons cases={[]} />)
    expect(screen.getByText('I want to')).toBeInTheDocument()
  })

  it('renders pill-shaped buttons', () => {
    render(<ActionButtons cases={[]} />)
    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      expect(link).toHaveClass('radius-pill')
    })
  })

  it('hides self-service CTAs when all cases are SNAP', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('SnapEbtCard')]} />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
  })

  it('hides self-service CTAs when all cases are TANF', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('TanfEbtCard')]} />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
  })

  it('shows self-service CTAs when at least one case is SummerEbt', () => {
    render(
      <ActionButtons
        cases={[makeCaseWithIssuance('SnapEbtCard'), makeCaseWithIssuance('SummerEbt')]}
      />
    )
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  it('shows info alert when self-service is unavailable', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('SnapEbtCard')]} />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('does not show info alert for SummerEbt cases', () => {
    render(<ActionButtons cases={[makeCaseWithIssuance('SummerEbt')]} />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  describe('DC state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })
    it('renders buttons with secondary background and ink text', () => {
      render(<ActionButtons cases={[]} />)
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
      render(<ActionButtons cases={[]} />)
      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-primary')
        expect(link).toHaveClass('text-white')
      })
    })
  })
})
