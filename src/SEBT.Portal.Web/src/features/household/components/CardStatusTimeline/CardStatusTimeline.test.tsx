import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { createMockApplication } from '../../testing'

import { CardStatusTimeline } from './CardStatusTimeline'

const mockApplication = createMockApplication({
  cardStatus: 'Active',
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardActivatedAt: '2026-01-08T00:00:00Z'
})

describe('CardStatusTimeline', () => {
  it('renders timeline heading', () => {
    render(<CardStatusTimeline application={mockApplication} />)

    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('renders nothing when cardStatus is null', () => {
    const { container } = render(
      <CardStatusTimeline application={{ ...mockApplication, cardStatus: null }} />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when cardStatus is Unknown', () => {
    const { container } = render(
      <CardStatusTimeline application={{ ...mockApplication, cardStatus: 'Unknown' }} />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders Active label for Active status', () => {
    render(<CardStatusTimeline application={mockApplication} />)
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('renders Requested label with date', () => {
    render(
      <CardStatusTimeline
        application={{
          ...mockApplication,
          cardStatus: 'Requested',
          cardMailedAt: null,
          cardActivatedAt: null
        }}
      />
    )
    expect(screen.getByText(/01\/01\/2026/)).toBeInTheDocument()
  })

  it('renders Issued label with date for Mailed status', () => {
    render(
      <CardStatusTimeline
        application={{ ...mockApplication, cardStatus: 'Mailed', cardActivatedAt: null }}
      />
    )
    expect(screen.getByText(/01\/03\/2026/)).toBeInTheDocument()
  })

  it('strips date placeholder when no date is available', () => {
    render(
      <CardStatusTimeline
        application={{ ...mockApplication, cardStatus: 'Processed', cardActivatedAt: null }}
      />
    )
    expect(screen.queryByText(/\[(?:MM\/DD\/YYYY|DD\/MM\/YYYY)\]/)).toBeNull()
  })

  it('renders Deactivated label for Deactivated status', () => {
    render(
      <CardStatusTimeline
        application={{
          ...mockApplication,
          cardStatus: 'Deactivated',
          cardDeactivatedAt: '2026-02-15T00:00:00Z'
        }}
      />
    )
    expect(screen.getByText('Deactivated')).toBeInTheDocument()
  })

  it('shows replacement card link when card is Processed', () => {
    render(
      <CardStatusTimeline
        application={{ ...mockApplication, cardStatus: 'Processed', cardActivatedAt: null }}
      />
    )
    expect(screen.getByRole('link')).toHaveTextContent('Request a replacement card')
  })

  it('does not show replacement card link when card is Active', () => {
    render(<CardStatusTimeline application={mockApplication} />)
    expect(screen.queryByRole('link')).toBeNull()
  })
})
