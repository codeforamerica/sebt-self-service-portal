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
    // Apply (always-on) + change address + request cards + check cards + check applications
    expect(links).toHaveLength(5)
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
    // Apply (always-on) + check cards + check applications remain; address & replacement are gated out
    expect(links).toHaveLength(3)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
  })

  it('shows only the Check existing applications CTA for a no-case household', () => {
    // A household with no enrolled cases: the backend evaluator denies address
    // and card actions, so denyAll is the realistic allowedActions shape here.
    render(
      <ActionButtons
        allowedActions={denyAll}
        hasCases={false}
      />
    )
    expect(screen.queryByText('Check existing cards')).toBeNull()
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
    expect(screen.getByText('Check existing applications')).toBeInTheDocument()
  })

  it('shows Check existing cards CTA when hasCases is true', () => {
    render(
      <ActionButtons
        allowedActions={allowAll}
        hasCases={true}
      />
    )
    expect(screen.getByText('Check existing cards')).toBeInTheDocument()
  })

  it('does not render the self-service-unavailable alert even when actions are denied', () => {
    render(<ActionButtons allowedActions={denyAll} />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('does not show info alert when all self-service actions are allowed', () => {
    render(<ActionButtons allowedActions={allowAll} />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows all CTAs when allowedActions is not provided (backward compatible)', () => {
    render(<ActionButtons />)
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(5)
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

  // The "Activate a card" CTA is state-gated: CO has the authored label, DC's is still
  // !N/A! upstream, so the entry is gated to CO until DC content lands. Tests assert on
  // href/data-analytics-cta rather than label text because the unit-test i18n bundle is
  // always DC (NEXT_PUBLIC_STATE=dc), so the CO-only label key never resolves here.
  describe('Activate a card CTA (DC-162)', () => {
    const activateLink = (container: HTMLElement) =>
      container.querySelector('a[href="/cards/activate"]')

    it('renders the activate-card CTA for CO when the household has cases', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(
        <ActionButtons
          allowedActions={allowAll}
          hasCases={true}
        />
      )
      const link = activateLink(container)
      expect(link).toBeInTheDocument()
      expect(link).toHaveAttribute('data-analytics-cta', 'activate_card_cta')
    })

    it('does not render the activate-card CTA for DC (label not yet published)', () => {
      mockGetState.mockReturnValue('dc')
      const { container } = render(
        <ActionButtons
          allowedActions={allowAll}
          hasCases={true}
        />
      )
      expect(activateLink(container)).toBeNull()
    })

    it('hides the activate-card CTA for CO when the household has no cases', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(
        <ActionButtons
          allowedActions={allowAll}
          hasCases={false}
        />
      )
      expect(activateLink(container)).toBeNull()
    })
  })

  // The apply CTA is an outbound link shown for both states, regardless of cases. The unit-test
  // i18n bundle is always DC, so the CO case asserts on href/cta rather than the label text.
  describe('Apply for benefits CTA', () => {
    const applyLink = (container: HTMLElement) =>
      container.querySelector('a[data-analytics-cta="apply_cta"]')

    it('renders the DC apply CTA linking to the DC apply form', () => {
      mockGetState.mockReturnValue('dc')
      render(<ActionButtons allowedActions={allowAll} />)
      const link = screen.getByText('Apply for DC SUN Bucks')
      expect(link).toHaveAttribute('href', 'https://forms.sunbucks.dc.gov/s3/AppUpdate2026')
      expect(link).toHaveAttribute('data-analytics-cta', 'apply_cta')
      expect(link).toHaveAttribute('data-analytics-cta-destination-type', 'external_only')
    })

    it('renders the CO apply CTA linking to the PEAK apply form', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(<ActionButtons allowedActions={allowAll} />)
      const link = applyLink(container)
      expect(link).toBeInTheDocument()
      expect(link?.getAttribute('href')).toContain(
        'peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'
      )
    })

    it('shows the apply CTA even when actions are denied and there are no cases', () => {
      const { container } = render(
        <ActionButtons
          allowedActions={denyAll}
          hasCases={false}
        />
      )
      expect(applyLink(container)).toBeInTheDocument()
    })
  })
})
