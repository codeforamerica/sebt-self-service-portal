import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import type { Application, Child } from '../../api'

import { ChildCard } from './ChildCard'

const mockChild: Child = {
  firstName: 'Sophia',
  lastName: 'Martinez'
}

const mockApplication: Application = {
  applicationNumber: 'APP-2026-001',
  caseNumber: 'CASE-DC-2026-001',
  applicationStatus: 'Approved',
  benefitIssueDate: '2026-01-08T00:00:00Z',
  benefitExpirationDate: '2026-03-19T00:00:00Z',
  last4DigitsOfCard: '1234',
  cardStatus: 'Active',
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardActivatedAt: '2026-01-08T00:00:00Z',
  cardDeactivatedAt: null,
  children: [mockChild],
  childrenOnApplication: 1
}

const defaultFlags: FeatureFlagsContextValue = {
  flags: TEST_FEATURE_FLAGS,
  isLoading: false,
  isError: false
}

function renderWithFlags(
  props: { child: Child; application: Application; id: string; defaultExpanded?: boolean },
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
    const applicationWithIssuanceType: Application = {
      ...mockApplication,
      issuanceType: 'SnapEbtCard'
    }

    renderWithFlags({
      child: mockChild,
      application: applicationWithIssuanceType,
      id: '0'
    })

    // Check for the card type heading (i18n key: cardTableHeadingCardType → "Benefit issued to")
    expect(screen.getByText('Benefit issued to')).toBeInTheDocument()
    // Check for the card type value (i18n key: cardTableTypeSnap → "Household SNAP EBT Card")
    expect(screen.getByText('Household SNAP EBT Card')).toBeInTheDocument()
  })

  it('does not render card type when issuanceType is null', () => {
    const applicationWithoutIssuanceType: Application = {
      ...mockApplication,
      issuanceType: null
    }

    renderWithFlags({
      child: mockChild,
      application: applicationWithoutIssuanceType,
      id: '0'
    })

    // Card type heading should not be present (i18n key: cardTableHeadingCardType → "Benefit issued to")
    expect(screen.queryByText('Benefit issued to')).not.toBeInTheDocument()
  })

  it('renders child name in accordion header', () => {
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0'
    })

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
  })

  it('renders benefit dates when provided', () => {
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0'
    })

    expect(screen.getByText('01/08/2026')).toBeInTheDocument()
    expect(screen.getByText('03/19/2026')).toBeInTheDocument()
  })

  it('renders card number when provided', () => {
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0'
    })

    expect(screen.getByText(/1234/)).toBeInTheDocument()
  })

  it('renders card status badge for CO-style cards (no cardRequestedAt)', () => {
    const coApplication: Application = {
      ...mockApplication,
      cardStatus: 'Active',
      cardRequestedAt: null
    }

    renderWithFlags({ child: mockChild, application: coApplication, id: '0' })

    expect(screen.getByTestId('card-status-badge')).toBeInTheDocument()
    expect(screen.queryByRole('list')).toBeNull()
  })

  it('renders card status timeline for DC-style cards (has cardRequestedAt)', () => {
    const dcApplication: Application = {
      ...mockApplication,
      cardStatus: 'Requested',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: null,
      cardActivatedAt: null
    }

    renderWithFlags({ child: mockChild, application: dcApplication, id: '0' })

    // DC-style: shows a single current-status row, not the CO badge
    expect(screen.queryByTestId('card-status-badge')).toBeNull()
    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('renders timeline for DC Active card (cardRequestedAt present)', () => {
    // DC Active cards have gone through Requested → Mailed → Active lifecycle
    const dcActiveApplication: Application = {
      ...mockApplication,
      cardStatus: 'Active',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: '2026-01-03T00:00:00Z',
      cardActivatedAt: '2026-01-08T00:00:00Z'
    }

    renderWithFlags({ child: mockChild, application: dcActiveApplication, id: '0' })

    expect(screen.queryByTestId('card-status-badge')).toBeNull()
    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('hides optional fields when not provided', () => {
    const minimalApplication: Application = {
      ...mockApplication,
      caseNumber: null,
      benefitIssueDate: null,
      benefitExpirationDate: null,
      last4DigitsOfCard: null,
      cardStatus: null,
      cardRequestedAt: null,
      cardMailedAt: null,
      cardActivatedAt: null,
      cardDeactivatedAt: null
    }

    renderWithFlags({
      child: mockChild,
      application: minimalApplication,
      id: '0'
    })

    // Should not show dates, card number, or card status timeline when not provided
    const definitionTerms = screen.queryAllByRole('term')
    expect(definitionTerms).toHaveLength(0)
  })

  it('sets aria-expanded to true when defaultExpanded is true', () => {
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0',
      defaultExpanded: true
    })

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'true')
  })

  it('sets aria-expanded to false when defaultExpanded is false', () => {
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '1',
      defaultExpanded: false
    })

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'false')
  })

  it('toggles accordion when button is clicked', async () => {
    const user = userEvent.setup()

    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0',
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
    renderWithFlags({
      child: mockChild,
      application: mockApplication,
      id: '0'
    })

    // i18n key: cardTableHeadingSebtId → "DC SUN Bucks ID" (DC) / "Summer EBT ID" (CO)
    expect(screen.getByText('DC SUN Bucks ID')).toBeInTheDocument()
    expect(screen.getByText('CASE-DC-2026-001')).toBeInTheDocument()
  })

  it('hides SEBT ID when show_case_number flag is off', () => {
    renderWithFlags(
      {
        child: mockChild,
        application: mockApplication,
        id: '0'
      },
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
      {
        child: mockChild,
        application: mockApplication,
        id: '0'
      },
      {
        flags: { ...TEST_FEATURE_FLAGS, show_card_last4: false },
        isLoading: false,
        isError: false
      }
    )

    expect(screen.queryByText(/1234/)).not.toBeInTheDocument()
  })
})
