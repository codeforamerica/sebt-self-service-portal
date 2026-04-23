import { render, screen } from '@testing-library/react'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

import enCODashboard from '@/content/locales/en/co/dashboard.json'
import { i18n } from '@sebt/design-system/client'

import type { CardStatus } from '../../api'
import { CardStatusDisplay } from './CardStatusDisplay'

// CardStatusDisplay is CO-specific. Tests default to the DC locale, so we
// add CO dashboard translations before the suite runs and remove them after.
beforeAll(() => {
  i18n.addResourceBundle('en', 'dashboard', enCODashboard, true, true)
})

afterAll(() => {
  i18n.removeResourceBundle('en', 'dashboard')
})

function renderWithStatus(cardStatus: CardStatus | null | undefined) {
  return render(<CardStatusDisplay cardStatus={cardStatus} />)
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

  it('renders nothing when cardStatus is Requested', () => {
    const { container } = renderWithStatus('Requested')

    expect(container.innerHTML).toBe('')
  })

  it('renders nothing when cardStatus is Mailed', () => {
    const { container } = renderWithStatus('Mailed')

    expect(container.innerHTML).toBe('')
  })

  it('renders Active status badge', () => {
    renderWithStatus('Active')

    // i18n key: cardTableStatusActive → "Active"
    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Active')
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

  it('renders Processed status badge with info styling', () => {
    renderWithStatus('Processed')

    const badge = screen.getByTestId('card-status-badge')
    // i18n key: cardTableStatusProcessed
    expect(badge).toHaveTextContent(/processed/i)
    expect(badge.className).toContain('bg-info-dark')
  })

  it('renders Inactive badge for Deactivated status', () => {
    renderWithStatus('Deactivated')

    expect(screen.getByTestId('card-status-badge')).toHaveTextContent('Inactive')
  })

  it('shows deactivated description for Deactivated status', () => {
    renderWithStatus('Deactivated')

    // i18n key: cardTableStatusMessageDeactivated
    expect(screen.getByText(/reported as lost, stolen, damaged/)).toBeInTheDocument()
  })

  it('does not show replacement card link for Processed status', () => {
    renderWithStatus('Processed')

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

  // --- CO denied-status fallback coverage ---
  // When the resolved locale string is empty (e.g. missing CSV content for a
  // given status), DESCRIPTION_FALLBACK supplies English copy so that the
  // "Card status" heading and a useful description still render. These tests
  // simulate the empty-locale condition by dropping the CO bundle we added in
  // beforeAll, leaving the default DC namespace (which has empty EN cells for
  // several of these keys) active for the test duration.

  describe('with empty locale entries (CSV content gap)', () => {
    beforeAll(() => {
      i18n.removeResourceBundle('en', 'dashboard')
      i18n.addResourceBundle(
        'en',
        'dashboard',
        {
          cardTableHeadingCardStatus: 'Card status',
          cardTableStatusInactive: 'Inactive',
          cardTableStatusUndeliverable: 'Undeliverable',
          cardTableStatusMessageDeactivated: '',
          cardTableStatusMessageUndeliverable: ''
        },
        true,
        true
      )
    })

    afterAll(() => {
      i18n.removeResourceBundle('en', 'dashboard')
      i18n.addResourceBundle('en', 'dashboard', enCODashboard, true, true)
    })

    it('renders fallback heading and body for NotActivated', () => {
      renderWithStatus('NotActivated')

      expect(screen.getByText('Card status')).toBeInTheDocument()
      expect(screen.getByText(/hasn't been activated yet/i)).toBeInTheDocument()
    })

    it('renders fallback heading and body for DeactivatedByState', () => {
      renderWithStatus('DeactivatedByState')

      expect(screen.getByText('Card status')).toBeInTheDocument()
      expect(screen.getByText(/state agency has deactivated this card/i)).toBeInTheDocument()
    })

    it('renders fallback heading and body for Undeliverable', () => {
      renderWithStatus('Undeliverable')

      expect(screen.getByText('Card status')).toBeInTheDocument()
      expect(screen.getByText(/returned as undeliverable/i)).toBeInTheDocument()
    })
  })

  // --- Truly-missing key coverage ---
  // i18next's default behavior is to return the key itself (a truthy string)
  // when a translation is missing. If the component's fallback chain relies on
  // a falsy value, it will render the raw key to the user instead of the
  // English fallback copy. These tests pin the component to the "missing-key
  // falls back to English copy" contract.

  describe('with entirely-missing locale keys', () => {
    beforeAll(() => {
      i18n.removeResourceBundle('en', 'dashboard')
      // Bundle intentionally omits every cardTableStatusMessage* key so each
      // lookup falls back to i18next's missing-key behavior.
      i18n.addResourceBundle(
        'en',
        'dashboard',
        {
          cardTableHeadingCardStatus: 'Card status',
          cardTableStatusActive: 'Active',
          cardTableStatusInactive: 'Inactive',
          cardTableStatusFrozen: 'Frozen',
          cardTableStatusUndeliverable: 'Undeliverable',
          cardTableStatusProcessed: 'Processed'
        },
        true,
        true
      )
    })

    afterAll(() => {
      i18n.removeResourceBundle('en', 'dashboard')
      i18n.addResourceBundle('en', 'dashboard', enCODashboard, true, true)
    })

    it('renders English fallback when cardTableStatusMessageActive key is absent', () => {
      renderWithStatus('Active')

      expect(screen.getByText(/This card has been sent to you/i)).toBeInTheDocument()
      expect(screen.queryByText('cardTableStatusMessageActive')).toBeNull()
    })

    it('renders English fallback when cardTableStatusMessageInactive key is absent', () => {
      renderWithStatus('Lost')

      expect(screen.getByText(/reported as lost, stolen, or damaged/i)).toBeInTheDocument()
      expect(screen.queryByText('cardTableStatusMessageInactive')).toBeNull()
    })

    it('renders English fallback when cardTableStatusMessageFrozen key is absent', () => {
      renderWithStatus('Frozen')

      expect(screen.getByText(/This card is frozen/i)).toBeInTheDocument()
      expect(screen.queryByText('cardTableStatusMessageFrozen')).toBeNull()
    })
  })
})
