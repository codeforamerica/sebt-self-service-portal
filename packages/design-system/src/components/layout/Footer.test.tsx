/**
 * Footer Component Unit Tests (CO)
 *
 * react-i18next is mocked so t(key) returns the key — we assert which key
 * each link requests and the href it points to. The help-desk mailto is
 * asserted against the links config (getStateLinks) rather than a literal
 * address, so the email never appears in test source.
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { getStateLinks } from '../../lib/links'

import { Footer } from './Footer'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' }
  })
}))

vi.mock('next/image', () => ({
  default: ({ alt, ...props }: { alt: string; [key: string]: unknown }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img alt={alt} {...props} />
  )
}))

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

describe('Footer (CO)', () => {
  it('renders the Summer EBT Help Desk link with the configured CDHS mailto', () => {
    render(<Footer state="co" />)
    // The full address can't be written into test source (a repo PII hook
    // blocks it), so we verify the mailto scheme and the CDHS domain here;
    // the exact address is configured in links.ts.
    const helpDeskHref = getStateLinks('co').help.helpDeskEmail ?? ''
    expect(helpDeskHref).toMatch(/^mailto:/)
    expect(helpDeskHref).toContain('state.co.us')
    expect(screen.getByRole('link', { name: 'titleContactUs' })).toHaveAttribute(
      'href',
      helpDeskHref
    )
  })

  it('renders the Digital accessibility statement link in the footer', () => {
    render(<Footer state="co" />)
    expect(screen.getByRole('link', { name: 'linkAccessibility' })).toHaveAttribute(
      'href',
      'https://cdhs.colorado.gov/accessibility-at-cdhs'
    )
  })

  it('does not leak the CO help-desk link into the DC footer', () => {
    render(<Footer state="dc" />)
    expect(screen.queryByRole('link', { name: 'titleContactUs' })).toBeNull()
  })
})
