import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { Application } from '../../api'
import { createMockApplication } from '../../testing'

import { CardStatusTimeline } from './CardStatusTimeline'

// Only override fields relevant to this test - card status timeline dates
const mockApplication = createMockApplication({
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardActivatedAt: '2026-01-08T00:00:00Z'
})

describe('CardStatusTimeline', () => {
  it('renders timeline heading', () => {
    render(<CardStatusTimeline application={mockApplication} />)

    // i18n key: cardTableHeadingCardStatus → "Card status"
    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('renders all three steps for active card', () => {
    render(<CardStatusTimeline application={mockApplication} />)

    // Timeline should have 3 steps (Requested, Mailed, Active)
    const steps = screen.getAllByRole('listitem')
    expect(steps).toHaveLength(3)
  })

  it('renders dates for completed steps', () => {
    render(<CardStatusTimeline application={mockApplication} />)

    expect(screen.getByText('01/01/2026')).toBeInTheDocument() // requested
    expect(screen.getByText('01/03/2026')).toBeInTheDocument() // mailed
    expect(screen.getByText('01/08/2026')).toBeInTheDocument() // activated
  })

  it('renders deactivated step when card is deactivated', () => {
    const deactivatedApplication: Application = {
      ...mockApplication,
      cardStatus: 'Deactivated',
      cardDeactivatedAt: '2026-02-15T00:00:00Z'
    }

    render(<CardStatusTimeline application={deactivatedApplication} />)

    // Should render 4 steps (Requested, Mailed, Active, Deactivated)
    const steps = screen.getAllByRole('listitem')
    expect(steps).toHaveLength(4)
    // Deactivation date should be visible
    expect(screen.getByText('02/15/2026')).toBeInTheDocument()
  })

  it('marks current step with aria-current', () => {
    render(<CardStatusTimeline application={mockApplication} />)

    const steps = screen.getAllByRole('listitem')
    // Active is the current step (index 2)
    expect(steps[2]).toHaveAttribute('aria-current', 'step')
  })

  it('renders nothing when no cardStatus', () => {
    const noCardApplication: Application = {
      ...mockApplication,
      cardStatus: null
    }

    const { container } = render(<CardStatusTimeline application={noCardApplication} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows only requested step when card is just requested', () => {
    const requestedApplication: Application = {
      ...mockApplication,
      cardStatus: 'Requested',
      cardMailedAt: null,
      cardActivatedAt: null
    }

    render(<CardStatusTimeline application={requestedApplication} />)

    const completedSteps = screen
      .getAllByRole('listitem')
      .filter((li) => li.classList.contains('usa-step-indicator__segment--complete'))

    expect(completedSteps).toHaveLength(1)
  })

  it('shows requested and mailed steps when card is mailed', () => {
    const mailedApplication: Application = {
      ...mockApplication,
      cardStatus: 'Mailed',
      cardActivatedAt: null
    }

    render(<CardStatusTimeline application={mailedApplication} />)

    const completedSteps = screen
      .getAllByRole('listitem')
      .filter((li) => li.classList.contains('usa-step-indicator__segment--complete'))

    expect(completedSteps).toHaveLength(2)
  })
})
