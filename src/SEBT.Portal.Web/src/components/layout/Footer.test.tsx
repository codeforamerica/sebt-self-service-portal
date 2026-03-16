import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { Footer } from './Footer'

// Mock react-i18next
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string, fallback?: string) => fallback ?? key,
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
  getStateLinks: (state: string) => {
    if (state === 'co') {
      return {
        footer: {
          transparencyOnline: '#',
          generalNotices: '#',
          digitalAccessibility: '#'
        },
        help: { helpDeskEmail: 'mailto:test@example.com' }
      }
    }
    return {
      footer: {
        publicNotifications: 'https://example.com/notifications',
        accessibility: '#',
        privacyAndSecurity: '#',
        googleTranslateDisclaimer: '#',
        about: '#',
        termsAndConditions: '#'
      },
      help: {}
    }
  },
  getFooterLinks: () => [
    { key: 'accessibility', href: '#', translationKey: 'linkAccessibility' },
    { key: 'privacy', href: '#', translationKey: 'linkPrivacyPolicy' }
  ]
}))

// Mock state module
vi.mock('@/lib/state', () => ({
  getStateConfig: (state: string) => ({
    name: state === 'co' ? 'Colorado' : 'District of Columbia',
    sealAlt:
      state === 'co'
        ? 'Colorado Official State Web Portal'
        : 'Government of the District of Columbia - Muriel Bowser, Mayor'
  })
}))

describe('Footer', () => {
  describe('DC Footer (default)', () => {
    it('should render the DC seal image', () => {
      render(<Footer state="dc" />)

      const seal = screen.getByAltText(
        'Government of the District of Columbia - Muriel Bowser, Mayor'
      )
      expect(seal).toBeInTheDocument()
      expect(seal).toHaveAttribute('src', '/images/states/dc/seal.svg')
    })

    it('should render the public notifications link', () => {
      render(<Footer state="dc" />)

      expect(screen.getByText('linkPublicNotices')).toBeInTheDocument()
    })

    it('should render footer navigation links', () => {
      render(<Footer state="dc" />)

      expect(screen.getByRole('navigation', { name: 'Footer navigation' })).toBeInTheDocument()
      expect(screen.getByText('linkAccessibility')).toBeInTheDocument()
      expect(screen.getByText('linkPrivacyPolicy')).toBeInTheDocument()
    })

    it('should render copyright text', () => {
      render(<Footer state="dc" />)

      expect(screen.getByText('copyrite')).toBeInTheDocument()
    })

    it('should apply teal background to the seal section', () => {
      render(<Footer state="dc" />)

      const seal = screen.getByAltText(
        'Government of the District of Columbia - Muriel Bowser, Mayor'
      )
      const sealSection = seal.closest('.usa-footer__primary-section')
      expect(sealSection).toHaveClass('bg-primary')
    })

    it('should apply teal background to the public notifications section', () => {
      render(<Footer state="dc" />)

      const notificationsLink = screen.getByText('linkPublicNotices')
      const section = notificationsLink.closest('.usa-footer__secondary-section')
      expect(section).toHaveClass('bg-primary')
    })

    it('should use white text for the public notifications link on dark background', () => {
      render(<Footer state="dc" />)

      const notificationsLink = screen.getByText('linkPublicNotices')
      expect(notificationsLink).toHaveClass('text-white')
    })

    it('should apply white background to the links navigation section', () => {
      render(<Footer state="dc" />)

      const nav = screen.getByRole('navigation', { name: 'Footer navigation' })
      const section = nav.closest('.usa-footer__secondary-section')
      expect(section).toHaveClass('bg-white')
    })

    it('should apply white background to the copyright section', () => {
      render(<Footer state="dc" />)

      const copyright = screen.getByText('copyrite')
      const section = copyright.closest('.usa-footer__secondary-section')
      expect(section).toHaveClass('bg-white')
    })
  })

  describe('CO Footer', () => {
    it('should render the CO seal image', () => {
      render(<Footer state="co" />)

      const seal = screen.getByAltText('Colorado Official State Web Portal')
      expect(seal).toBeInTheDocument()
      expect(seal).toHaveAttribute('src', '/images/states/co/seal.svg')
    })

    it('should render copyright with inline links', () => {
      render(<Footer state="co" />)

      expect(screen.getByText('© 2026 State of Colorado', { exact: false })).toBeInTheDocument()
      expect(screen.getByText('Transparency Online')).toBeInTheDocument()
      expect(screen.getByText('General Notices')).toBeInTheDocument()
    })

    it('should apply light gray background to the copyright bar', () => {
      render(<Footer state="co" />)

      const copyright = screen.getByText('© 2026 State of Colorado', { exact: false })
      const section = copyright.closest('.usa-footer__primary-section')
      expect(section).toHaveClass('bg-base-lighter')
    })

    it('should apply white background to the seal section', () => {
      render(<Footer state="co" />)

      const seal = screen.getByAltText('Colorado Official State Web Portal')
      const section = seal.closest('.usa-footer__secondary-section')
      expect(section).toHaveClass('bg-white')
    })
  })
})
