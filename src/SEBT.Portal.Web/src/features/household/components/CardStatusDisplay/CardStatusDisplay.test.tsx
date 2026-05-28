import { render, screen } from '@testing-library/react'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

import enCODashboard from '@/content/locales/en/co/dashboard.json'
import enDCDashboard from '@/content/locales/en/dc/dashboard.json'
import { i18n } from '@sebt/design-system/client'

import type { CardStatus } from '../../api'
import { CardStatusDisplay } from './CardStatusDisplay'

// CardStatusDisplay is CO-specific. Tests default to the DC locale, so we
// add CO dashboard translations before the suite runs and remove them after.
beforeAll(() => {
  // CO provides most card status keys; DC provides cardTableStatusIssued (Processed is DC-only)
  i18n.addResourceBundle('en', 'dashboard', { ...enCODashboard, ...enDCDashboard }, true, true)
})

afterAll(() => {
  i18n.removeResourceBundle('en', 'dashboard')
})

function renderWithStatus(cardStatus: CardStatus | null | undefined, cardIssuedAt?: string | null) {
  return render(
    <CardStatusDisplay
      cardStatus={cardStatus}
      cardIssuedAt={cardIssuedAt}
    />
  )
}

describe('CardStatusDisplay', () => {
  it('renders nothing when cardStatus is null', () => {
    const { container } = renderWithStatus(null)

    expect(container.innerHTML).toBe('')
  })

  it('renders nothing when cardStatus is Unknown', () => {
    const { container } = renderWithStatus('Unknown')

    expect(container.innerHTML).toBe('')
  })

  it('renders Active status badge', () => {
    renderWithStatus('Active')

    // i18n key: cardTableStatusActive → "Active"
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Active')
    expect(screen.getByTestId('card-status-badge').className).toContain('info')
  })

  it('renders Inactive badge for Lost status', () => {
    renderWithStatus('Lost')

    // i18n key: cardTableStatusInactive → "Inactive"
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('renders Inactive badge for Stolen status', () => {
    renderWithStatus('Stolen')

    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('renders Inactive badge for Damaged status', () => {
    renderWithStatus('Damaged')

    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('renders Inactive badge for DeactivatedByState', () => {
    renderWithStatus('DeactivatedByState')

    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('renders Inactive badge for NotActivated', () => {
    renderWithStatus('NotActivated')

    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('renders Processed status badge with interpolated issue date', () => {
    renderWithStatus('Processed', '2026-01-15T00:00:00Z')

    // cardTableStatusIssued → "Issued on [MM/DD/YYYY]"; interpolateDate substitutes the date
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Issued on 01/15/2026')
  })

  it('renders Processed status badge with success styling', () => {
    renderWithStatus('Processed', '2026-01-15T00:00:00Z')

    expect(screen.getByTestId('card-status-badge').className).toContain('success')
  })

  it('does not show replacement card link for Processed status', () => {
    renderWithStatus('Processed', '2026-01-15T00:00:00Z')

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('renders Frozen status badge', () => {
    renderWithStatus('Frozen')

    // i18n key: cardTableStatusFrozen → "Frozen"
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Frozen')
  })

  it('renders Undeliverable status badge', () => {
    renderWithStatus('Undeliverable')

    // i18n key: cardTableStatusUndeliverable → "Undeliverable"
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Undeliverable')
    expect(screen.getByTestId('card-status-badge').className).toContain('warning')
  })

  // ── Replacement link ──
  // CardStatusDisplay does not render replacement links (ChildCard handles this)

  it('does not render replacement link for Lost status', () => {
    renderWithStatus('Lost')

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('does not render replacement link for Active status', () => {
    renderWithStatus('Active')

    expect(screen.queryByRole('link')).toBeNull()
  })

  // ── Description text ──

  it('shows inactive description for Lost/Stolen/Damaged', () => {
    renderWithStatus('Lost')

    // i18n key: cardTableStatusMessageInactive
    expect(screen.getByText(/reported as lost, stolen, damaged/)).toBeInTheDocument()
  })

  it('shows deactivated description for DeactivatedByState', () => {
    renderWithStatus('DeactivatedByState')

    // i18n key: cardTableStatusMessageDeactivated
    expect(screen.getByText(/reported as lost, stolen, damaged/)).toBeInTheDocument()
  })
})
