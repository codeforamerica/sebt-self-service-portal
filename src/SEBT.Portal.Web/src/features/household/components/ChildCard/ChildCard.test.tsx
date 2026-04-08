import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import type { SummerEbtCase } from '../../api'
import { createMockSummerEbtCase } from '../../testing'

import { ChildCard } from './ChildCard'

const mockCase: SummerEbtCase = createMockSummerEbtCase({
  summerEBTCaseID: 'SEBT-001',
  applicationId: 'APP-2026-001',
  childFirstName: 'Sophia',
  childLastName: 'Martinez',
  ebtCaseNumber: 'CASE-DC-2026-001',
  ebtCardLastFour: '1234',
  ebtCardStatus: 'Active',
  benefitAvailableDate: '2026-01-08T00:00:00Z',
  benefitExpirationDate: '2026-03-19T00:00:00Z',
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardActivatedAt: '2026-01-08T00:00:00Z',
  cardDeactivatedAt: null
})

const defaultFlags: FeatureFlagsContextValue = {
  flags: TEST_FEATURE_FLAGS,
  isLoading: false,
  isError: false
}

function renderWithFlags(
  props: { summerEbtCase: SummerEbtCase; defaultExpanded?: boolean },
  flags: FeatureFlagsContextValue = defaultFlags
) {
  return render(
    <FeatureFlagsContext.Provider value={flags}>
      <ChildCard {...props} />
    </FeatureFlagsContext.Provider>
  )
}

describe('ChildCard', () => {
  it('renders card type when issuanceType is provided', () => {
    const caseWithIssuanceType = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: 'SnapEbtCard'
    })

    renderWithFlags({ summerEbtCase: caseWithIssuanceType })

    // Check for the card type heading (i18n key: cardTableHeadingCardType → "Benefit issued to")
    expect(screen.getByText('Benefit issued to')).toBeInTheDocument()
    // Check for the card type value (i18n key: cardTableTypeSnap → "Household SNAP EBT Card")
    expect(screen.getByText('Household SNAP EBT Card')).toBeInTheDocument()
  })

  it('does not render card type when issuanceType is null', () => {
    const caseWithoutIssuanceType = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: null
    })

    renderWithFlags({ summerEbtCase: caseWithoutIssuanceType })

    // Card type heading should not be present (i18n key: cardTableHeadingCardType → "Benefit issued to")
    expect(screen.queryByText('Benefit issued to')).not.toBeInTheDocument()
  })

  it('renders child name in accordion header', () => {
    renderWithFlags({ summerEbtCase: mockCase })

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
  })

  it('renders benefit dates when provided', () => {
    renderWithFlags({ summerEbtCase: mockCase })

    expect(screen.getByText('01/08/2026')).toBeInTheDocument()
    expect(screen.getByText('03/19/2026')).toBeInTheDocument()
  })

  it('renders card number when provided', () => {
    renderWithFlags({ summerEbtCase: mockCase })

    expect(screen.getByText(/1234/)).toBeInTheDocument()
  })

  it('renders card status badge for CO-style cards (no cardRequestedAt)', () => {
    const coCase = createMockSummerEbtCase({
      ...mockCase,
      ebtCardStatus: 'Active',
      cardRequestedAt: null
    })

    renderWithFlags({ summerEbtCase: coCase })

    expect(screen.getByTestId('card-status-badge')).toBeInTheDocument()
    expect(screen.queryByRole('list')).toBeNull()
  })

  it('renders card status timeline for DC-style cards (has cardRequestedAt)', () => {
    const dcCase = createMockSummerEbtCase({
      ...mockCase,
      ebtCardStatus: 'Requested',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: null,
      cardActivatedAt: null
    })

    renderWithFlags({ summerEbtCase: dcCase })

    // DC-style: shows a single current-status row, not the CO badge
    expect(screen.queryByTestId('card-status-badge')).toBeNull()
    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('renders timeline for DC Active card (cardRequestedAt present)', () => {
    // DC Active cards have gone through Requested → Mailed → Active lifecycle
    const dcActiveCase = createMockSummerEbtCase({
      ...mockCase,
      ebtCardStatus: 'Active',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: '2026-01-03T00:00:00Z',
      cardActivatedAt: '2026-01-08T00:00:00Z'
    })

    renderWithFlags({ summerEbtCase: dcActiveCase })

    expect(screen.queryByTestId('card-status-badge')).toBeNull()
    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('hides optional fields when not provided', () => {
    const minimalCase = createMockSummerEbtCase({
      ...mockCase,
      ebtCaseNumber: null,
      benefitAvailableDate: null,
      benefitExpirationDate: null,
      ebtCardLastFour: null,
      ebtCardStatus: null,
      issuanceType: null,
      cardRequestedAt: null,
      cardMailedAt: null,
      cardActivatedAt: null,
      cardDeactivatedAt: null
    })

    renderWithFlags({ summerEbtCase: minimalCase })

    // Should not show dates, card number, or card status timeline when not provided
    const definitionTerms = screen.queryAllByRole('term')
    expect(definitionTerms).toHaveLength(0)
  })

  it('sets aria-expanded to true when defaultExpanded is true', () => {
    renderWithFlags({
      summerEbtCase: mockCase,
      defaultExpanded: true
    })

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'true')
  })

  it('sets aria-expanded to false when defaultExpanded is false', () => {
    renderWithFlags({
      summerEbtCase: mockCase,
      defaultExpanded: false
    })

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'false')
  })

  it('toggles accordion when button is clicked', async () => {
    const user = userEvent.setup()

    renderWithFlags({
      summerEbtCase: mockCase,
      defaultExpanded: true
    })

    const button = screen.getByRole('button')
    const content = screen.getByTestId('accordion-content')

    // Initially expanded
    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(content).not.toHaveAttribute('hidden')

    // Click to collapse
    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(content).toHaveAttribute('hidden')

    // Click to expand again
    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(content).not.toHaveAttribute('hidden')
  })

  it('renders SEBT ID when caseNumber is provided', () => {
    renderWithFlags({ summerEbtCase: mockCase })

    // i18n key: cardTableHeadingSebtId → "DC SUN Bucks ID" (DC) / "Summer EBT ID" (CO)
    expect(screen.getByText('DC SUN Bucks ID')).toBeInTheDocument()
    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
  })

  it('hides SEBT ID when show_case_number flag is off', () => {
    renderWithFlags(
      { summerEbtCase: mockCase },
      {
        flags: { ...TEST_FEATURE_FLAGS, show_case_number: false },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText('DC SUN Bucks ID')).not.toBeInTheDocument()
    expect(screen.queryByText('CASE-DC-2026-001')).not.toBeInTheDocument()
  })

  it('hides card number when show_card_last4 flag is off', () => {
    renderWithFlags(
      { summerEbtCase: mockCase },
      {
        flags: { ...TEST_FEATURE_FLAGS, show_card_last4: false },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText(/1234/)).not.toBeInTheDocument()
  })

  it('does not show replacement link when issuanceType is null', () => {
    const caseNoType = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: null
    })

    renderWithFlags(
      { summerEbtCase: caseNoType },
      {
        flags: { ...TEST_FEATURE_FLAGS, enable_card_replacement: true },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText('Request a replacement card')).not.toBeInTheDocument()
  })

  it('does not show replacement link when issuanceType is Unknown', () => {
    const caseUnknown = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: 'Unknown'
    })

    renderWithFlags(
      { summerEbtCase: caseUnknown },
      {
        flags: { ...TEST_FEATURE_FLAGS, enable_card_replacement: true },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText('Request a replacement card')).not.toBeInTheDocument()
  })

  it('shows replacement link for SummerEbt when feature flag is enabled', () => {
    const summerEbtCase = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: 'SummerEbt',
      cardRequestedAt: '2025-01-01T00:00:00Z'
    })

    renderWithFlags(
      { summerEbtCase },
      {
        flags: { ...TEST_FEATURE_FLAGS, enable_card_replacement: true },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.getByText('Request a replacement card')).toBeInTheDocument()
  })

  it('hides replacement link when enable_card_replacement flag is off', () => {
    const summerEbtCase = createMockSummerEbtCase({
      ...mockCase,
      issuanceType: 'SummerEbt',
      cardRequestedAt: '2025-01-01T00:00:00Z'
    })

    renderWithFlags(
      { summerEbtCase },
      {
        flags: { ...TEST_FEATURE_FLAGS, enable_card_replacement: false },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText('Request a replacement card')).not.toBeInTheDocument()
  })
})
