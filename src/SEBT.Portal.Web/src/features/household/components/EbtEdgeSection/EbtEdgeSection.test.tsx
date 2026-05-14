import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { EbtEdgeSection } from './EbtEdgeSection'

describe('EbtEdgeSection', () => {
  it('renders section with accessible heading', () => {
    render(<EbtEdgeSection />)

    const section = screen.getByRole('region')
    expect(section).toHaveAttribute('aria-labelledby', 'help-section-heading')
  })

  it('renders accordion heading with translated text', () => {
    render(<EbtEdgeSection />)

    // i18n key: alertEbtEdgeTitle → "Check balance or change PIN number"
    expect(screen.getByText('Check balance or change PIN number')).toBeInTheDocument()
  })

  it('renders accordion button with correct aria attributes', () => {
    render(<EbtEdgeSection />)

    const button = screen.getByRole('button')
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(button).toHaveAttribute('aria-controls', 'help-content')
  })

  it('renders help content hidden by default', () => {
    render(<EbtEdgeSection />)

    const content = document.getElementById('help-content')
    expect(content).toHaveAttribute('hidden')
  })

  it('toggles accordion when button is clicked', async () => {
    const user = userEvent.setup()
    const { container } = render(<EbtEdgeSection />)

    const button = screen.getByRole('button')
    const content = container.querySelector('#help-content')!

    // Initially collapsed
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(content).toHaveAttribute('hidden')

    // Click to expand
    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(content).not.toHaveAttribute('hidden')

    // Click to collapse
    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(content).toHaveAttribute('hidden')
  })

  it('renders ebtEDGE link with external attributes', () => {
    render(<EbtEdgeSection />)

    // i18n key: alertEbtEdgeAction → "Go to ebtEDGE"
    const ebtLink = screen.getByText('Go to ebtEDGE')
    expect(ebtLink).toHaveAttribute('href', 'https://www.ebtedge.com')
    expect(ebtLink).toHaveAttribute('target', '_blank')
    expect(ebtLink).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('renders help body content', () => {
    render(<EbtEdgeSection />)

    // i18n key: alertEbtEdgeBody → "If you need to"
    expect(screen.getByText('If you need to')).toBeInTheDocument()
  })

  it('renders features as bullet points when expanded', async () => {
    const user = userEvent.setup()
    render(<EbtEdgeSection />)

    // Expand the accordion first
    await user.click(screen.getByRole('button'))

    // i18n key: alertEbtEdgeFeatures contains newline-separated features
    const list = screen.getByRole('list')
    expect(list).toBeInTheDocument()
    expect(screen.getByText(/Check the card balance/)).toBeInTheDocument()
  })
})
