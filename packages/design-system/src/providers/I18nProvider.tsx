'use client'

import { useEffect } from 'react'
import { I18nextProvider } from 'react-i18next'

import i18n, { supportedLanguages, type SupportedLanguage } from '../lib/i18n'

import type { I18nProviderProps } from './types'

export function I18nProvider({ children }: I18nProviderProps) {
  // Sync language from localStorage after hydration to prevent SSR mismatch
  useEffect(() => {
    const stored = localStorage.getItem('i18nextLng')
    if (stored && supportedLanguages.includes(stored as SupportedLanguage)) {
      i18n.changeLanguage(stored)
      document.documentElement.lang = stored
    }
  }, [])

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
}
