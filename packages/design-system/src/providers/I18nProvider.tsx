'use client'

import { useEffect } from 'react'
import { I18nextProvider } from 'react-i18next'

import i18n, { supportedLanguages, type SupportedLanguage } from '../lib/i18n'

import type { I18nProviderProps } from './types'

export function I18nProvider({ children }: I18nProviderProps) {
  // Pick the user's language after hydration to prevent SSR mismatch.
  // Precedence: ?lang=<code> URL param > localStorage > default ('en').
  // The URL param lets users deep-link to a non-English experience
  // (e.g. ?lang=es); when present and supported it also persists to
  // localStorage so the choice sticks across subsequent navigations.
  useEffect(() => {
    const fromUrl = new URLSearchParams(window.location.search).get('lang')
    const stored = localStorage.getItem('i18nextLng')
    const isSupported = (lng: string | null): lng is SupportedLanguage =>
      lng !== null && supportedLanguages.includes(lng as SupportedLanguage)

    const chosen = isSupported(fromUrl) ? fromUrl : isSupported(stored) ? stored : null
    if (!chosen) return

    i18n.changeLanguage(chosen)
    document.documentElement.lang = chosen
    if (isSupported(fromUrl)) {
      localStorage.setItem('i18nextLng', chosen)
    }
  }, [])

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
}
