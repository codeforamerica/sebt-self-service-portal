import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext } from '@/features/feature-flags'

import type { AllowedActions } from '../../api'

import { ActionButtons } from './ActionButtons'

function withApplyFlag(children: ReactNode, enableApply: boolean) {
  return (
    <FeatureFlagsContext.Provider
      value={{ flags: { enable_apply: enableApply }, isLoading: false, isError: false }}
    >
      {children}
    </FeatureFlagsContext.Provider>
  )
}

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
    // Change address + request cards + check cards + check applications.
    // Apply is hidden for DC (applications closed); see the Apply describe below.
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
    // Check cards + check applications remain; address & replacement are gated out, apply is DC-hidden
    expect(links).toHaveLength(2)
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

  it('hides Check existing applications CTA when hasApplications is false', () => {
    // Co-loaded households have cases but no applications; the CTA scrolls to an
    // applications section that does not render, so it must hide (DC-402).
    render(
      <ActionButtons
        allowedActions={allowAll}
        hasCases={true}
        hasApplications={false}
      />
    )
    expect(screen.queryByText('Check existing applications')).toBeNull()
    expect(screen.getByText('Check existing cards')).toBeInTheDocument()
  })

  it('shows Check existing applications CTA when hasApplications is true', () => {
    render(
      <ActionButtons
        allowedActions={allowAll}
        hasCases={true}
        hasApplications={true}
      />
    )
    expect(screen.getByText('Check existing applications')).toBeInTheDocument()
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

  // The apply CTA is an outbound link driven by useApplyHref: it renders only when the
  // enable_apply flag is on AND the state has an apply destination (CO's PEAK form; DC
  // has none since DC-701). The unit-test i18n bundle is always DC, so the CO cases
  // assert on href/cta attributes rather than the label text.
  describe('Apply for benefits CTA', () => {
    const applyLink = (container: HTMLElement) =>
      container.querySelector('a[data-analytics-cta="apply_cta"]')

    it('does not render the apply CTA for DC even when applications are open', () => {
      mockGetState.mockReturnValue('dc')
      const { container } = render(withApplyFlag(<ActionButtons allowedActions={allowAll} />, true))
      expect(applyLink(container)).toBeNull()
      expect(screen.queryByText('Apply for DC SUN Bucks')).toBeNull()
    })

    it('renders the CO apply CTA linking to the PEAK apply form when applications are open', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(withApplyFlag(<ActionButtons allowedActions={allowAll} />, true))
      const link = applyLink(container)
      expect(link).toBeInTheDocument()
      expect(link?.getAttribute('href')).toContain(
        'peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'
      )
      expect(link).toHaveAttribute('data-analytics-cta-destination-type', 'external_only')
    })

    it('does not render the CO apply CTA when the enable_apply flag is off', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(
        withApplyFlag(<ActionButtons allowedActions={allowAll} />, false)
      )
      expect(applyLink(container)).toBeNull()
    })

    it('does not render the CO apply CTA outside the feature-flags provider (fail closed)', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(<ActionButtons allowedActions={allowAll} />)
      expect(applyLink(container)).toBeNull()
    })

    it('shows the CO apply CTA even when actions are denied and there are no cases', () => {
      mockGetState.mockReturnValue('co')
      const { container } = render(
        withApplyFlag(
          <ActionButtons
            allowedActions={denyAll}
            hasCases={false}
          />,
          true
        )
      )
      expect(applyLink(container)).toBeInTheDocument()
    })
  })
})
