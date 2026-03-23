import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { ActionButtons } from './ActionButtons'

describe('ActionButtons', () => {
  it('renders navigation element with aria-label', () => {
    render(<ActionButtons />)

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons for SummerEbt', () => {
    render(<ActionButtons issuanceType="SummerEbt" />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(4)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '/cards')
  })

  it('renders request replacement cards button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders check applications button', () => {
    render(<ActionButtons />)

    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '/applications')
  })

  it('renders "I want to" heading', () => {
    render(<ActionButtons />)

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

  // ── Self-service eligibility ──

  it('hides self-service CTAs for SNAP issuance type', () => {
    render(<ActionButtons issuanceType="SnapEbtCard" />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
    expect(screen.queryByText('Change my mailing address')).toBeNull()
    expect(screen.queryByText('Request new cards')).toBeNull()
  })

  it('hides self-service CTAs for TANF issuance type', () => {
    render(<ActionButtons issuanceType="TanfEbtCard" />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(2)
  })

  it('shows info alert when self-service is unavailable', () => {
    render(<ActionButtons issuanceType="SnapEbtCard" />)

    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('does not show info alert for SummerEbt', () => {
    render(<ActionButtons issuanceType="SummerEbt" />)

    expect(screen.queryByRole('status')).toBeNull()
  })
})
