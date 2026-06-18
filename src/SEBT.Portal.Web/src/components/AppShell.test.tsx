import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { AppShell } from './AppShell'

const mockPathname = vi.fn(() => '/login')

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname()
}))

vi.mock('@/components/BetaBanner', () => ({
  BetaBanner: () => <div data-testid="beta-banner" />
}))

vi.mock('@/components/OutageGuard', () => ({
  OutageGuard: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  OUTAGE_PATH: '/outage'
}))

vi.mock('@sebt/design-system/client', () => ({
  Header: () => <header data-testid="site-header" />,
  HelpSection: () => <section data-testid="help-section" />,
  Footer: () => <footer data-testid="site-footer" />
}))

describe('AppShell', () => {
  beforeEach(() => {
    mockPathname.mockReturnValue('/login')
  })

  it('renders portal chrome on normal routes', () => {
    render(
      <AppShell state="dc">
        <div>Portal page</div>
      </AppShell>
    )

    expect(screen.getByTestId('beta-banner')).toBeInTheDocument()
    expect(screen.getByTestId('site-header')).toBeInTheDocument()
    expect(screen.getByTestId('help-section')).toBeInTheDocument()
    expect(screen.getByTestId('site-footer')).toBeInTheDocument()
    expect(screen.getByText('Portal page')).toBeInTheDocument()
  })

  it('reserves full-viewport height on normal routes to prevent loading layout shift', () => {
    render(
      <AppShell state="dc">
        <div>Portal page</div>
      </AppShell>
    )

    // minh-viewport (min-height: 100vh) keeps the main box a full viewport tall
    // during the loading state, so content filling in does not shift the footer
    // and the page below (the DC-310 Cumulative Layout Shift fix).
    expect(document.getElementById('main-content')).toHaveClass('minh-viewport')
  })

  it('renders a minimal shell on the outage route', () => {
    mockPathname.mockReturnValue('/outage')

    render(
      <AppShell state="dc">
        <div>Outage page</div>
      </AppShell>
    )

    expect(screen.queryByTestId('site-header')).not.toBeInTheDocument()
    expect(screen.queryByTestId('help-section')).not.toBeInTheDocument()
    expect(screen.queryByTestId('site-footer')).not.toBeInTheDocument()
    expect(screen.getByText('Outage page')).toBeInTheDocument()
    expect(document.getElementById('main-content')).toHaveClass('bg-base-lightest')
  })
})
