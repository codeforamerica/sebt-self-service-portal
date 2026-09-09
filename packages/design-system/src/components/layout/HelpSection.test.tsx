/**
 * HelpSection Component Unit Tests (CO)
 *
 * react-i18next is mocked so t(key) returns the key. We assert the CO
 * variant renders the FAQ teaser and no longer renders the help-desk
 * email or the accessibility paragraph.
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { HelpSection } from './HelpSection'

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

describe('HelpSection (CO)', () => {
  it('renders the FAQ heading and a learn-more link to the CDHS FAQ page', () => {
    render(<HelpSection state="co" />)
    expect(screen.getByRole('heading', { name: 'titleFaqs' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'linkFaqs' })).toHaveAttribute(
      'href',
      'https://cdhs.colorado.gov/summer-ebt-faq'
    )
  })

  it('no longer renders the help-desk email or the accessibility paragraph', () => {
    render(<HelpSection state="co" />)
    expect(screen.queryByText('linkContactUs2')).toBeNull()
    expect(screen.queryByText('bodyAccessibility')).toBeNull()
    expect(screen.queryByRole('heading', { name: 'titleAccessibility' })).toBeNull()
  })
})
