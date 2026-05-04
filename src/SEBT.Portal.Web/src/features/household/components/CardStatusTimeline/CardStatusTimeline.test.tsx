import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { CardStatusTimeline } from './CardStatusTimeline'

describe('CardStatusTimeline', () => {
  it('renders the card-status heading', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    expect(screen.getByText(/card status/i)).toBeInTheDocument()
  })

  it('shows the requested-on label with interpolated date', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-15T00:00:00Z" />)
    expect(screen.getByText(/requested on 01\/15\/2026/i)).toBeInTheDocument()
  })

  it('shows the cooldown reassurance message', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    expect(screen.getByText(/arrive in the mail within 2–3 weeks/i)).toBeInTheDocument()
  })

  it('uses state-neutral wording in the message fallback', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    expect(screen.queryByText(/DC SUN Bucks/i)).not.toBeInTheDocument()
  })

  it('renders without a date when cardRequestedAt is null', () => {
    render(<CardStatusTimeline cardRequestedAt={null} />)
    expect(screen.getByText(/card status/i)).toBeInTheDocument()
    // interpolateDate strips the connector + placeholder when no date present:
    // "Requested on [MM/DD/YYYY]" → "Requested"
    expect(screen.queryByText(/\[MM\/DD\/YYYY\]/i)).toBeNull()
    expect(screen.getByText('Requested')).toBeInTheDocument()
  })
})
