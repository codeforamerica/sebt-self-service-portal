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
  languageNames: {
    en: 'English',
    es: 'Español',
    am: 'አማርኛ'
  }
}))

// The active language is mutable so a test can place the user in a language the
// current state does not offer. Held via vi.hoisted so the mock factory can read
// it without hitting a temporal-dead-zone error on import.
const i18nState = vi.hoisted(() => ({ language: 'en' }))

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
    i18n: i18nState
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
    i18nState.language = 'en'
  })

  /**
   * The language list must come from the `state` prop, not from a deployment-wide
   * default resolved at module import. Colorado offers English and Spanish only and
   * has no Amharic content, so an Amharic option there renders as an extra focusable
   * control with no accessible name that a screen reader announces as a third choice.
   *
   * These assertions target the rendered menu rather than the config behind it: the
   * defect can be introduced by wiring alone, while every config-level assertion
   * still passes.
   */
  describe('State-Driven Language Resolution', () => {
    it('offers only English and Spanish for CO when no languages prop is given', () => {
      render(<LanguageSelector state="co" />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const buttons = nav.querySelectorAll('button')

      expect(buttons).toHaveLength(2)
      expect(buttons[0]).toHaveAttribute('lang', 'en')
      expect(buttons[1]).toHaveAttribute('lang', 'es')
      expect(nav.querySelector('[lang="am"]')).toBeNull()
    })

    it('offers only two menu items for CO in the mobile menu', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector state="co" />)

      await user.click(screen.getByRole('button', { name: /translate/i }))

      const items = screen.getAllByRole('menuitem')

      expect(items).toHaveLength(2)
      items.forEach((item) => {
        expect(item).not.toHaveAttribute('lang', 'am')
      })
    })

    it('omits Amharic from the collapsed translate subtitle for CO', () => {
      render(<LanguageSelector state="co" />)

      // The subtitle is built from `languageCodes`, which is a separate prop from
      // the menu's `languages`. Resolving one but not the other would leave this
      // stale while the expanded menu looked correct.
      const translateButton = screen.getByRole('button', { name: /translate/i })

      expect(translateButton).toHaveTextContent('English')
      expect(translateButton).toHaveTextContent('Español')
      expect(translateButton.querySelector('[lang="am"]')).toBeNull()
    })

    it('gives every rendered language option a non-empty accessible name', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector state="co" />)

      await user.click(screen.getByRole('button', { name: /translate/i }))

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      const options = [...nav.querySelectorAll('button'), ...screen.getAllByRole('menuitem')]

      expect(options.length).toBeGreaterThan(0)
      options.forEach((option) => {
        expect(option.textContent?.trim()).not.toBe('')
      })
    })

    it('still offers all three languages for DC', async () => {
      const user = userEvent.setup()
      render(<LanguageSelector state="dc" />)

      const nav = screen.getByRole('navigation', { name: 'Language selector' })
      expect(nav.querySelectorAll('button')).toHaveLength(3)
      expect(nav.querySelector('[lang="am"]')).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: /translate/i }))
      expect(screen.getAllByRole('menuitem')).toHaveLength(3)
    })

    it('offers no Amharic option for CO even when the active language is Amharic', async () => {
      // Reachable before the per-state config landed, via a persisted
      // `i18nextLng=am` value carried onto a CO origin.
      i18nState.language = 'am'
      const user = userEvent.setup()
      render(<LanguageSelector state="co" />)

      await user.click(screen.getByRole('button', { name: /translate/i }))

      const items = screen.getAllByRole('menuitem')
      expect(items).toHaveLength(2)
      items.forEach((item) => {
        expect(item.textContent?.trim()).not.toBe('')
      })
      // No option matches the active language, so none is marked current. See the
      // open question in questions.md about whether that is the desired behavior.
      expect(document.querySelector('[aria-current="true"]')).toBeNull()
    })
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
      // Languages are passed explicitly so the expected count is stated here
      // rather than inherited from whichever state the default resolves to.
      render(<LanguageSelector languages={['en', 'es'] as const} />)

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
      render(<LanguageSelector languages={['en', 'es'] as const} />)

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
