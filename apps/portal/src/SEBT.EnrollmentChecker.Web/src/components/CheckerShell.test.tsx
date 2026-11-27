import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { CheckerShell } from './CheckerShell'

const mockPathname = vi.fn(() => '/')

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname()
}))

vi.mock('@/components/OutageGuard', () => ({
  OutageGuard: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  OUTAGE_PATH: '/outage'
}))

// These tests cover which chrome the shell renders, not what either guard decides.
// The outage guard passes children straight through; the season gate is switchable
// so the tests can show what waits on it.
let mockSeasonResolving = false

vi.mock('@/components/SeasonGate', () => ({
  SeasonGate: ({ children }: { children: React.ReactNode }) =>
    mockSeasonResolving ? null : <>{children}</>
}))

vi.mock('@/features/maintenance', () => ({
  MaintenanceBanner: () => <div data-testid="maintenance-banner" />
}))

vi.mock('@sebt/design-system/src/components/layout/Header', () => ({
  Header: () => <header data-testid="site-header" />
}))

vi.mock('@sebt/design-system/src/components/layout/HelpSection', () => ({
  HelpSection: () => <section data-testid="help-section" />
}))

vi.mock('@sebt/design-system/src/components/layout/Footer', () => ({
  Footer: () => <footer data-testid="site-footer" />
}))

vi.mock('@sebt/design-system/src/components/layout/SkipNav', () => ({
  SkipNav: () => <div data-testid="skip-nav" />
}))

describe('CheckerShell', () => {
  beforeEach(() => {
    mockPathname.mockReturnValue('/')
    mockSeasonResolving = false
  })

  // Anything painted below an empty main gets pushed down when the content lands,
  // so the help section and footer wait on the same gate as the page content.
  it('holds the page content, help section and footer together while the season resolves', () => {
    mockSeasonResolving = true

    render(
      <CheckerShell state="co">
        <div>Checker page</div>
      </CheckerShell>
    )

    expect(screen.getByTestId('site-header')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.queryByText('Checker page')).not.toBeInTheDocument()
    expect(screen.queryByTestId('help-section')).not.toBeInTheDocument()
    expect(screen.queryByTestId('site-footer')).not.toBeInTheDocument()
  })

  it('renders the help section and footer on the outage route without waiting on the season', () => {
    mockSeasonResolving = true
    mockPathname.mockReturnValue('/outage')

    render(
      <CheckerShell state="co">
        <div>Outage page</div>
      </CheckerShell>
    )

    expect(screen.getByText('Outage page')).toBeInTheDocument()
    expect(screen.getByTestId('help-section')).toBeInTheDocument()
    expect(screen.getByTestId('site-footer')).toBeInTheDocument()
  })

  it('renders checker chrome on normal routes', () => {
    render(
      <CheckerShell state="co">
        <div>Checker page</div>
      </CheckerShell>
    )

    expect(screen.getByTestId('skip-nav')).toBeInTheDocument()
    expect(screen.getByTestId('maintenance-banner')).toBeInTheDocument()
    expect(screen.getByTestId('site-header')).toBeInTheDocument()
    expect(screen.getByTestId('help-section')).toBeInTheDocument()
    expect(screen.getByTestId('site-footer')).toBeInTheDocument()
    expect(screen.getByText('Checker page')).toBeInTheDocument()
  })

  it('keeps full chrome on the outage route but drops the maintenance banner', () => {
    mockPathname.mockReturnValue('/outage')

    render(
      <CheckerShell state="co">
        <div>Outage page</div>
      </CheckerShell>
    )

    expect(screen.getByTestId('site-header')).toBeInTheDocument()
    expect(screen.getByTestId('help-section')).toBeInTheDocument()
    expect(screen.getByTestId('site-footer')).toBeInTheDocument()
    expect(screen.queryByTestId('maintenance-banner')).not.toBeInTheDocument()
    expect(screen.getByText('Outage page')).toBeInTheDocument()
  })
})
