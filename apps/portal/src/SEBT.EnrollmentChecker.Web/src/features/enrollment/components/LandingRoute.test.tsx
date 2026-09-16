import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { LandingRoute } from './LandingRoute'

let mockSeasonState = { season: 'open' as 'open' | 'closed', isResolving: false }

vi.mock('@/lib/useEnrollmentSeason', () => ({
  useEnrollmentSeason: () => mockSeasonState
}))

// Which page the route picks is the whole behaviour here; what each page renders
// is covered by its own suite.
vi.mock('./LandingPage', () => ({
  LandingPage: () => <div data-testid="landing-page" />
}))

vi.mock('./ClosedPage', () => ({
  ClosedPage: () => <div data-testid="closed-page" />
}))

describe('LandingRoute', () => {
  beforeEach(() => {
    mockSeasonState = { season: 'open', isResolving: false }
  })

  it('serves the open-season landing page while the season is enrolling', () => {
    render(<LandingRoute />)
    expect(screen.getByTestId('landing-page')).toBeInTheDocument()
    expect(screen.queryByTestId('closed-page')).toBeNull()
  })

  it('serves the post-season page once the season has closed', () => {
    mockSeasonState = { season: 'closed', isResolving: false }
    render(<LandingRoute />)
    expect(screen.getByTestId('closed-page')).toBeInTheDocument()
    expect(screen.queryByTestId('landing-page')).toBeNull()
  })
})
