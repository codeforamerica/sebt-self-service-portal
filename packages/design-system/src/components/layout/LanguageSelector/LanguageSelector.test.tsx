/**
 * LanguageSelector Component Unit Tests
 *
 * Tests the language selector behavior including:
 * - Desktop: horizontal button list with language switching
 * - Mobile: accordion dropdown with open/close behavior
 * - Keyboard navigation (Escape to close)
 * - Click outside to close
 * - Accessibility attributes
 */
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { LanguageSelector } from './LanguageSelector'

// Mock i18n module
const mockChangeLanguage = vi.fn()
vi.mock('../../../lib/i18n', () => ({
  changeLanguage: (lang: string) => mockChangeLanguage(lang),
  supportedLanguages: ['en', 'es'] as const,
  languageNames: {
    en: 'English',
    es: 'Español',
    am: 'አማርኛ'
  }
}))

// Mock react-i18next
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const translations: Record<string, string> = {
        languageSelector: 'Language selector',
        translate: 'Translate',
        english: 'English',
        español: 'Español',
        amharic: 'አማርኛ'
      }
      // eslint-disable-next-line security/detect-object-injection -- key is typed, not user input
      return translations[key] ?? key
    },
    i18n: {
      language: 'en'
    }
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

describe('LanguageSelector', () => {
  beforeEach(() => {
    mockChangeLanguage.mockClear()
  })

  describe('Rendering', () => {
    it('should render both desktop and mobile selectors', () => {
      render(<LanguageSelector />)

      // Desktop selector (nav with list)
      expect(screen.getByRole('navigation', { name: 'Language selector' })).toBeInTheDocument()

      // Mobile selector (accordion button)
      expect(screen.getByRole('button', { name: /translate/i })).toBeInTheDocument()
    })

    it('should render with default state prop for icon path', () => {
      render(<LanguageSelector />)

      // Icon has aria-hidden so we query by selector
      const icon = document.querySelector('img[src*="translate_Rounded"]')
      expect(icon).toHaveAttribute('src', '/images/states/dc/icons/translate_Rounded.svg')
    })

    it('should render with custom state prop', () => {
      render(<LanguageSelector state="co" />)

      const icon = document.querySelector('img[src*="translate_Rounded"]')
      expect(icon).toHaveAttribute('src', '/images/states/co/icons/translate_Rounded.svg')
    })

    it('should render mobile menu as hidden by default', () => {
      render(<LanguageSelector />)

      const menu = screen.getByRole('menu', { hidden: true })
      expect(menu).toHaveAttribute('hidden')
      expect(menu).toHaveAttribute('aria-hidden', 'true')
    })
  })

  describe('Desktop Language Switching', () => {
    it('should render all language buttons in desktop nav', () => {
      render(<LanguageSelector />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const buttons = nav.querySelectorAll('button')

      expect(buttons).toHaveLength(2)
      expect(buttons[0]).toHaveAttribute('lang', 'en')
      expect(buttons[1]).toHaveAttribute('lang', 'es')
    })

    it('should call changeLanguage when clicking a desktop language button', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const spanishButton = nav.querySelector('button[lang="es"]')

      await user.click(spanishButton!)

      expect(mockChangeLanguage).toHaveBeenCalledWith('es')
      expect(mockChangeLanguage).toHaveBeenCalledTimes(1)
    })

    it('should mark current language with aria-current in desktop', () => {
      render(<LanguageSelector />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const englishButton = nav.querySelector('button[lang="en"]')
      const spanishButton = nav.querySelector('button[lang="es"]')

      expect(englishButton).toHaveAttribute('aria-current', 'true')
      expect(spanishButton).not.toHaveAttribute('aria-current')
    })
  })

  describe('Mobile Accordion Behavior', () => {
    it('should open menu when clicking translate button', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })
      await user.click(translateButton)

      const menu = screen.getByRole('menu')
      expect(menu).not.toHaveAttribute('hidden')
      expect(menu).toHaveAttribute('aria-hidden', 'false')
    })

    it('should toggle aria-expanded on translate button', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })
      expect(translateButton).toHaveAttribute('aria-expanded', 'false')

      await user.click(translateButton)
      expect(translateButton).toHaveAttribute('aria-expanded', 'true')

      await user.click(translateButton)
      expect(translateButton).toHaveAttribute('aria-expanded', 'false')
    })

    it('should display available languages in mobile button', () => {
      render(<LanguageSelector />)

      // The mobile button shows language names - use getAllByText since they appear in both views
      expect(screen.getAllByText('English').length).toBeGreaterThan(0)
      expect(screen.getAllByText('Español').length).toBeGreaterThan(0)
    })
  })

  describe('Mobile Language Selection', () => {
    it('should call changeLanguage when selecting from mobile menu', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      // Open menu
      await user.click(screen.getByRole('button', { name: /translate/i }))

      // Select Spanish
      const spanishOption = screen.getByRole('menuitem', { name: 'Español' })
      await user.click(spanishOption)

      expect(mockChangeLanguage).toHaveBeenCalledWith('es')
    })

    it('should close menu after selecting a language', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      // Open menu
      await user.click(screen.getByRole('button', { name: /translate/i }))
      expect(screen.getByRole('menu')).not.toHaveAttribute('hidden')

      // Select language
      await user.click(screen.getByRole('menuitem', { name: 'Español' }))

      // Menu should be closed
      expect(screen.getByRole('menu', { hidden: true })).toHaveAttribute('hidden')
    })

    it('should mark current language with aria-current in mobile menu', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      await user.click(screen.getByRole('button', { name: /translate/i }))

      expect(screen.getByRole('menuitem', { name: 'English' })).toHaveAttribute(
        'aria-current',
        'true'
      )
      expect(screen.getByRole('menuitem', { name: 'Español' })).not.toHaveAttribute('aria-current')
    })
  })

  describe('Keyboard Navigation', () => {
    it('should close mobile menu when pressing Escape', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })

      // Open menu
      await user.click(translateButton)
      expect(screen.getByRole('menu')).not.toHaveAttribute('hidden')

      // Press Escape
      await user.keyboard('{Escape}')

      // Menu should be closed
      expect(screen.getByRole('menu', { hidden: true })).toHaveAttribute('hidden')
    })

    it('should return focus to translate button after Escape', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })

      await user.click(translateButton)
      await user.keyboard('{Escape}')

      expect(translateButton).toHaveFocus()
    })

    it('should return focus to translate button after selecting a language', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })

      await user.click(translateButton)
      await user.click(screen.getByRole('menuitem', { name: 'Español' }))

      expect(translateButton).toHaveFocus()
    })

    it('should be keyboard accessible in desktop view', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      // Tab to first desktop button and press Enter
      await user.tab()
      await user.keyboard('{Enter}')

      expect(mockChangeLanguage).toHaveBeenCalled()
    })
  })

  describe('Click Outside', () => {
    it('should close mobile menu when clicking outside', async () => {
      const user = userEvent.setup()

      render(
        <div>
          <button data-testid="outside">Outside</button>
          <LanguageSelector />
        </div>
      )

      // Open menu
      await user.click(screen.getByRole('button', { name: /translate/i }))
      expect(screen.getByRole('menu')).not.toHaveAttribute('hidden')

      // Click outside
      await user.click(screen.getByTestId('outside'))

      // Menu should be closed
      expect(screen.getByRole('menu', { hidden: true })).toHaveAttribute('hidden')
    })
  })

  describe('Custom Languages Prop', () => {
    it('should render only provided languages in desktop nav', () => {
      render(<LanguageSelector languages={['en'] as const} />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const buttons = nav.querySelectorAll('button')

      expect(buttons).toHaveLength(1)
      expect(buttons[0]).toHaveAttribute('lang', 'en')
    })

    it('should handle three languages', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector languages={['en', 'es', 'am'] as const} />)

      // Desktop should have 3 buttons
      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      expect(nav.querySelectorAll('button')).toHaveLength(3)

      // Open mobile menu - should have 3 items
      await user.click(screen.getByRole('button', { name: /translate/i }))
      expect(screen.getAllByRole('menuitem')).toHaveLength(3)
    })
  })

  describe('Accessibility', () => {
    it('should have proper ARIA attributes for mobile accordion', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      const translateButton = screen.getByRole('button', { name: /translate/i })
      expect(translateButton).toHaveAttribute('aria-expanded', 'false')
      expect(translateButton).toHaveAttribute('aria-controls', 'language-options')

      await user.click(translateButton)
      expect(translateButton).toHaveAttribute('aria-expanded', 'true')
    })

    it('should use menu and menuitem roles in mobile view', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      await user.click(screen.getByRole('button', { name: /translate/i }))

      expect(screen.getByRole('menu')).toBeInTheDocument()
      expect(screen.getAllByRole('menuitem')).toHaveLength(2)
    })

    it('should set lang attribute on all language buttons', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector />)

      // Desktop buttons
      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      expect(nav.querySelector('button[lang="en"]')).toBeInTheDocument()
      expect(nav.querySelector('button[lang="es"]')).toBeInTheDocument()

      // Mobile menu items
      await user.click(screen.getByRole('button', { name: /translate/i }))
      expect(screen.getByRole('menuitem', { name: 'English' })).toHaveAttribute('lang', 'en')
      expect(screen.getByRole('menuitem', { name: 'Español' })).toHaveAttribute('lang', 'es')
    })

    it('should use button type="button" to prevent form submission', () => {
      render(<LanguageSelector />)

      const allButtons = screen.getAllByRole('button')
      allButtons.forEach((button) => {
        expect(button).toHaveAttribute('type', 'button')
      })
    })
  })
})
