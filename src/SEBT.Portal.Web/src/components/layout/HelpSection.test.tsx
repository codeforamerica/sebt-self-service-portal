import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { HelpSection } from './HelpSection'

// Track t() calls to verify translation key usage
const tSpy = vi.fn((key: string, fallback?: string) => fallback ?? key)

// Mock react-i18next
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: tSpy,
    i18n: { language: 'en' }
  })
}))

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ alt, ...props }: { alt: string; [key: string]: unknown }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      alt={alt}
      {...props}
    />
  )
}))

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

// Mock links module
vi.mock('@/lib/links', () => ({
  getStateLinks: () => ({
    footer: { digitalAccessibility: '#' },
    help: { helpDeskEmail: 'mailto:test@example.com' }
  }),
  getHelpLinks: () => [
    { key: 'faqs', href: '#', translationKey: 'linkFaqs', icon: 'faqs-icon.svg' },
    { key: 'contactUs', href: '#', translationKey: 'linkContactUs', icon: 'contact-icon.svg' }
  ]
}))

describe('HelpSection', () => {
  describe('CO HelpSection', () => {
    it('should use t() for the contact us heading', () => {
      tSpy.mockClear()
      render(<HelpSection state="co" />)

      expect(tSpy).toHaveBeenCalledWith('titleContactUs')
    })

    it('should use t() for the accessibility heading', () => {
      tSpy.mockClear()
      render(<HelpSection state="co" />)

      expect(tSpy).toHaveBeenCalledWith('titleAccessibility')
    })

    it('should use t() for the accessibility body text', () => {
      tSpy.mockClear()
      render(<HelpSection state="co" />)

      expect(tSpy).toHaveBeenCalledWith('bodyAccessibility')
    })

    it('should render the help desk email link', () => {
      render(<HelpSection state="co" />)

      const emailLink = screen.getByRole('link', { name: /cdhs_sebt_supportcenter/i })
      expect(emailLink).toHaveAttribute('href', 'mailto:test@example.com')
    })

    it('should render the digital accessibility button', () => {
      render(<HelpSection state="co" />)

      const button = screen.getByRole('link', { name: /digital accessibility/i })
      expect(button).toBeInTheDocument()
    })
  })
})
