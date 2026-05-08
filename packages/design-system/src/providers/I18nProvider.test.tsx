/**
 * I18nProvider Tests
 *
 * Verifies the post-hydration language selection precedence:
 *   ?lang=<code> URL param  >  localStorage 'i18nextLng'  >  default
 *
 * The URL-param branch (DC-365) lets users deep-link to a non-English
 * experience (e.g. ?lang=es). When present and valid, the param also
 * gets persisted to localStorage so the choice sticks across navigations.
 */
import { render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { I18nProvider } from './I18nProvider'

const mockChangeLanguage = vi.fn()

vi.mock('../lib/i18n', () => ({
  default: {
    // Minimal i18next surface used by I18nextProvider + our effect.
    changeLanguage: (lng: string) => mockChangeLanguage(lng),
    language: 'en',
    use: () => ({ init: () => Promise.resolve() }),
    on: () => {},
    off: () => {},
    t: (key: string) => key,
    services: { resourceStore: { data: {} } },
    options: {},
    isInitialized: true,
    hasResourceBundle: () => true,
    getFixedT: () => (key: string) => key
  },
  supportedLanguages: ['en', 'es', 'am'] as const
}))

vi.mock('react-i18next', () => ({
  I18nextProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>
}))

function setSearch(search: string) {
  // jsdom lets us mutate location.search via History API
  window.history.replaceState({}, '', `/${search}`)
}

describe('I18nProvider', () => {
  beforeEach(() => {
    mockChangeLanguage.mockClear()
    localStorage.clear()
    setSearch('')
    document.documentElement.lang = ''
  })

  afterEach(() => {
    setSearch('')
  })

  describe('localStorage rehydration', () => {
    it('applies a stored language when no URL param is present', () => {
      localStorage.setItem('i18nextLng', 'es')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).toHaveBeenCalledWith('es')
      expect(document.documentElement.lang).toBe('es')
    })

    it('ignores an unsupported stored language', () => {
      localStorage.setItem('i18nextLng', 'klingon')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).not.toHaveBeenCalled()
    })

    it('does nothing when no stored language and no URL param', () => {
      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).not.toHaveBeenCalled()
    })
  })

  describe('?lang= URL parameter', () => {
    it('applies a supported language from the URL', () => {
      setSearch('?lang=es')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).toHaveBeenCalledWith('es')
      expect(document.documentElement.lang).toBe('es')
    })

    it('persists the URL language to localStorage so the choice sticks', () => {
      setSearch('?lang=am')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(localStorage.getItem('i18nextLng')).toBe('am')
    })

    it('takes precedence over a conflicting localStorage value', () => {
      localStorage.setItem('i18nextLng', 'es')
      setSearch('?lang=am')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).toHaveBeenCalledWith('am')
      expect(mockChangeLanguage).not.toHaveBeenCalledWith('es')
      expect(localStorage.getItem('i18nextLng')).toBe('am')
    })

    it('ignores an unsupported URL value and falls back to localStorage', () => {
      localStorage.setItem('i18nextLng', 'es')
      setSearch('?lang=klingon')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).toHaveBeenCalledWith('es')
      // The bad URL value must not poison localStorage.
      expect(localStorage.getItem('i18nextLng')).toBe('es')
    })

    it('ignores an unsupported URL value when localStorage is empty', () => {
      setSearch('?lang=klingon')

      render(
        <I18nProvider>
          <div />
        </I18nProvider>
      )

      expect(mockChangeLanguage).not.toHaveBeenCalled()
      expect(localStorage.getItem('i18nextLng')).toBeNull()
    })
  })
})
