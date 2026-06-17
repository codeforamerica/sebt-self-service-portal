/**
 * Header Component Unit Tests
 *
 * Tests that the state logo renders with the correct translation key
 * for its alt text. The `react-i18next` mock returns the key unchanged,
 * so asserting on the rendered `alt` value proves the component requests
 * the right key.
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { Header } from './Header'

// Mock react-i18next: t(key) returns the key so we can assert which key
// the component asked for.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' }
  })
}))

// Mock the LanguageSelector so this test stays focused on the logo/header.
vi.mock('./LanguageSelector', () => ({
  LanguageSelector: () => <div data-testid="language-selector" />
}))

// Mock next/image to a plain img so the alt attribute is observable.
vi.mock('next/image', () => ({
  default: ({ alt, ...props }: { alt: string; [key: string]: unknown }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      alt={alt}
      {...props}
    />
  )
}))

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

describe('Header', () => {
  it('renders the state logo with the bannerImageAltText key for its alt text', () => {
    render(<Header state="dc" />)

    expect(screen.getByRole('img')).toHaveAttribute('alt', 'bannerImageAltText')
  })
})
