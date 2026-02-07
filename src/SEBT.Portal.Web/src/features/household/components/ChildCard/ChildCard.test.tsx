import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Application, Child } from '../../api'

import { ChildCard } from './ChildCard'

const mockChild: Child = {
  caseNumber: 456001,
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

describe('ChildCard', () => {
  it('renders card type when issuanceType is provided', () => {
    const applicationWithIssuanceType: Application = {
      ...mockApplication,
      issuanceType: 'SnapEbtCard'
    }

    render(
      <ChildCard
        child={mockChild}
        application={applicationWithIssuanceType}
        id="0"
      />
    )

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

    render(
      <ChildCard
        child={mockChild}
        application={applicationWithoutIssuanceType}
        id="0"
      />
    )

    // Card type heading should not be present (i18n key: cardTableHeadingCardType → "Benefit issued to")
    expect(screen.queryByText('Benefit issued to')).not.toBeInTheDocument()
  })

  it('renders child name in accordion header', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
      />
    )

    expect(screen.getByText('Sophia Martinez')).toBeInTheDocument()
  })

  it('renders benefit dates when provided', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
      />
    )

    // benefitIssueDate may appear in both ChildCard and CardStatusTimeline (when same as cardActivatedAt)
    expect(screen.getAllByText('01/08/2026').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('03/19/2026')).toBeInTheDocument()
  })

  it('renders card number when provided', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
      />
    )

    expect(screen.getByText(/1234/)).toBeInTheDocument()
  })

  it('renders card status timeline when card status is provided', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
      />
    )

    // CardStatusTimeline renders with heading and steps
    // i18n key: cardTableHeadingCardStatus → "Card status"
    expect(screen.getByText('Card status')).toBeInTheDocument()
    // Timeline step indicator list should be present
    expect(screen.getByRole('list', { name: 'Card status timeline' })).toBeInTheDocument()
  })

  it('hides optional fields when not provided', () => {
    const minimalApplication: Application = {
      ...mockApplication,
      benefitIssueDate: null,
      benefitExpirationDate: null,
      last4DigitsOfCard: null,
      cardStatus: null,
      cardRequestedAt: null,
      cardMailedAt: null,
      cardActivatedAt: null,
      cardDeactivatedAt: null
    }

    render(
      <ChildCard
        child={mockChild}
        application={minimalApplication}
        id="0"
      />
    )

    // Should not show dates, card number, or card status timeline when not provided
    const definitionTerms = screen.queryAllByRole('term')
    expect(definitionTerms).toHaveLength(0)
  })

  it('sets aria-expanded to true when defaultExpanded is true', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
        defaultExpanded={true}
      />
    )

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'true')
  })

  it('sets aria-expanded to false when defaultExpanded is false', () => {
    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="1"
        defaultExpanded={false}
      />
    )

    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'false')
  })

  it('toggles accordion when button is clicked', async () => {
    const user = userEvent.setup()

    render(
      <ChildCard
        child={mockChild}
        application={mockApplication}
        id="0"
        defaultExpanded={true}
      />
    )

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
})
