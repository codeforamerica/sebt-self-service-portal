import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { CardStatus } from '../../api'

import { CardStatusTimeline } from './CardStatusTimeline'

const defaultProps = {
  cardStatus: 'Active' as CardStatus,
  cardRequestedAt: '2026-01-01T00:00:00Z',
  cardMailedAt: '2026-01-03T00:00:00Z',
  cardDeactivatedAt: null
}

describe('CardStatusTimeline', () => {
  it('renders timeline heading', () => {
    render(<CardStatusTimeline {...defaultProps} />)

    expect(screen.getByText('Card status')).toBeInTheDocument()
  })

  it('renders nothing when cardStatus is null', () => {
    const { container } = render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={null}
      />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when cardStatus is Unknown', () => {
    const { container } = render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Unknown'}
      />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders Active label for Active status', () => {
    render(<CardStatusTimeline {...defaultProps} />)
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('renders Requested label with date', () => {
    render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Requested'}
        cardMailedAt={null}
      />
    )
    expect(screen.getByText(/01\/01\/2026/)).toBeInTheDocument()
  })

  it('renders Issued label with date for Mailed status', () => {
    render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Mailed'}
      />
    )
    expect(screen.getByText(/01\/03\/2026/)).toBeInTheDocument()
  })

  it('strips date placeholder when no date is available', () => {
    render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Processed'}
      />
    )
    expect(screen.queryByText(/\[(?:MM\/DD\/YYYY|DD\/MM\/YYYY)\]/)).toBeNull()
  })

  it('renders Deactivated label for Deactivated status', () => {
    render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Deactivated'}
        cardDeactivatedAt={'2026-02-15T00:00:00Z'}
      />
    )
    expect(screen.getByText('Deactivated')).toBeInTheDocument()
  })

  it('does not render replacement link (ChildCard handles replacement links)', () => {
    render(
      <CardStatusTimeline
        {...defaultProps}
        cardStatus={'Processed'}
      />
    )
    expect(screen.queryByRole('link')).toBeNull()
  })
})
