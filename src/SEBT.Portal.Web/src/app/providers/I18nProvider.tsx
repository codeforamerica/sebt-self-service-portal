'use client'

/**
 * I18n Provider Component
 *
 * Client-side wrapper for react-i18next's I18nextProvider.
 * Must be a client component since i18next uses React context.
 *
 * Handles language synchronization after hydration to prevent SSR/client mismatch:
 * 1. Server renders with 'en' (default)
 * 2. Client hydrates with 'en' (matches server)
 * 3. After mount, useEffect syncs to stored language from localStorage
 *
 * Usage:
 *   <I18nProvider>
 *     {children}
 *   </I18nProvider>
 */

import { useEffect } from 'react'
import { I18nextProvider } from 'react-i18next'

import i18n, { supportedLanguages, type SupportedLanguage } from '@/src/lib/i18n'

interface I18nProviderProps {
  children: React.ReactNode
}

export function I18nProvider({ children }: I18nProviderProps) {
  useEffect(() => {
    // After hydration, sync language from localStorage
    const stored = localStorage.getItem('i18nextLng')
    if (stored && supportedLanguages.includes(stored as SupportedLanguage)) {
      i18n.changeLanguage(stored)
      document.documentElement.lang = stored
    }
  }, [])

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
}
