import { render, screen } from '@testing-library/react'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

import { i18n } from '@sebt/design-system/client'

import { CardStatusTimeline } from './CardStatusTimeline'

beforeAll(() => {
  i18n.addResourceBundle(
    'en',
    'dashboard',
    {
      cardTableHeadingCardStatus: 'Card status',
      cardTableStatusRequested: 'Requested on [MM/DD/YYYY]',
      cardTableStatusMessageRequested2:
        'A replacement card will arrive in the mail in around 7-10 business days.'
    },
    true,
    true
  )
})

afterAll(() => {
  i18n.removeResourceBundle('en', 'dashboard')
})

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
    expect(screen.getByText(/arrive in the mail in around 7-10 business days/i)).toBeInTheDocument()
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
