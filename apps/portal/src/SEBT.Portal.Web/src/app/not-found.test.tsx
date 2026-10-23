import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { getStateLinks } from '@sebt/design-system'

import enDc404 from '@/content/locales/en/dc/404Portal.json'
import NotFound from './not-found'

// Wiring test: renders against the portal's real i18next instance (initialized
// in test-setup from the generated locale resources), proving the 404Portal
// namespace flows through end to end. Hrefs are asserted against getStateLinks
// rather than literals so no contact address appears in test source.

describe('NotFound', () => {
  it('renders the 404 copy from the generated locale bundle', () => {
    render(<NotFound />)

    const headings = screen.getAllByRole('heading', { level: 1 })
    expect(headings).toHaveLength(1)
    expect(headings[0]).toHaveTextContent(enDc404.title)
    expect(screen.getByText(enDc404.body1)).toBeInTheDocument()
  })

  it('titles the page in the state color and size', () => {
    render(<NotFound />)
    // DC's pageTitleText; CO resolves to text-primary for the teal in its mockup
    expect(screen.getByRole('heading', { level: 1 })).toHaveClass('font-sans-xl', 'text-ink')
  })

  it('offers the dashboard as the outline action', () => {
    render(<NotFound />)

    const dashboard = screen.getByRole('link', { name: enDc404.action1 })
    expect(dashboard).toHaveAttribute('href', '/dashboard')
    expect(dashboard).toHaveClass('usa-button', 'usa-button--outline')
  })

  it('offers contact us as the filled action, tagged as an external destination', () => {
    render(<NotFound />)

    const contactUs = screen.getByRole('link', { name: enDc404.action2 })
    expect(contactUs).toHaveAttribute('href', getStateLinks('dc').help.contactUs)
    expect(contactUs).toHaveClass('usa-button')
    expect(contactUs).not.toHaveClass('usa-button--outline')
    expect(contactUs).toHaveAttribute('data-analytics-cta-destination-type', 'external_only')
  })

  it('neutralizes the USWDS default trailing margin so the row fits at mobile width', () => {
    render(<NotFound />)
    expect(screen.getByRole('link', { name: enDc404.action2 })).toHaveClass('margin-right-0')
  })
})
