import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { SeasonGate } from './SeasonGate'

let mockSeasonState = { season: 'open' as const, isResolving: false }

vi.mock('@/lib/useEnrollmentSeason', () => ({
  useEnrollmentSeason: () => mockSeasonState
}))

const renderGate = () =>
  render(
    <SeasonGate>
      <p>page content</p>
    </SeasonGate>
  )

describe('SeasonGate', () => {
  beforeEach(() => {
    mockSeasonState = { season: 'open', isResolving: false }
  })

  // The season decides which page this is, so showing one and swapping it for
  // the other a moment later is worse than a brief hold.
  it('withholds content while the season is still resolving', () => {
    mockSeasonState = { season: 'open', isResolving: true }
    renderGate()
    expect(screen.queryByText('page content')).toBeNull()
  })

  it('renders content once the season is known', () => {
    renderGate()
    expect(screen.getByText('page content')).toBeInTheDocument()
  })
})
