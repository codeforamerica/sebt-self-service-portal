import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { ActionButtons } from './ActionButtons'

describe('ActionButtons', () => {
  it('renders navigation element with aria-label', () => {
    render(<ActionButtons />)

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(5)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationCheckExistingCards
    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '/cards')
  })

  it('renders request new cards button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationOrderReplacementCards
    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationChangeMyMailingAddress
    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders change contact info button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationChangeMyContactInformation
    const link = screen.getByText('Change my contact information')
    expect(link).toHaveAttribute('href', '/profile/contact')
  })

  it('renders check applications button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationCheckExistingApplications
    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '/applications')
  })

  it('renders "I want to" heading', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationLead
    expect(screen.getByText('I want to')).toBeInTheDocument()
  })

  it('uses pill-shaped solid gold button styling', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      expect(link).toHaveClass('radius-pill')
      expect(link).toHaveClass('bg-secondary')
    })
  })

  it('renders chevron icon after each button text', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      const svg = link.querySelector('svg')
      expect(svg).toBeInTheDocument()
      expect(svg).toHaveAttribute('aria-hidden', 'true')
    })
  })
})
