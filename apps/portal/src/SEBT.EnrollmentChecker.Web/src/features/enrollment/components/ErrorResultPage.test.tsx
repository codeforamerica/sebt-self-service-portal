import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { ErrorResultPage } from './ErrorResultPage'

const portalUrl = 'https://portal.example.gov'

describe('ErrorResultPage', () => {
  it('renders the error title and body', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'Something went wrong on our end' })
    ).toBeInTheDocument()
    expect(screen.getByText(/You may try to check again later/)).toBeInTheDocument()
  })

  it('offers the portal step with the portal link', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(screen.getByText(/received their benefits and when they expire/)).toBeInTheDocument()
    expect(screen.getByTestId('portal-link')).toHaveAttribute('href', portalUrl)
  })

  it('shows no application steps or links', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(screen.queryByTestId('apply-2027-link')).toBeNull()
    expect(screen.queryByTestId('apply-for-sebt-link')).toBeNull()
    expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
  })
})
