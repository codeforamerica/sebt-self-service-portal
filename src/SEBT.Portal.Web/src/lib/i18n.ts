/**
 * i18n Configuration for SEBT Portal
 *
 * Uses react-i18next with state-specific locale namespaces.
 * Translations are loaded dynamically based on the current state (DC, CO).
 *
 * Key Features:
 * - State-specific translations: Each state has its own copy
 * - Namespace-based: Translations split by page/component for lazy loading
 * - Language detection: Browser preference with fallback to English
 * - Runtime interpolation: Supports {state}, {year}, {stateName} variables
 *
 * Directory Structure:
 *   content/locales/{locale}/{state}/{namespace}.json
 *   e.g., content/locales/en/dc/landing.json
 */

import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// Import locale files statically for SSR support
// English - DC
import enDCCommon from '@/content/locales/en/dc/common.json'
import enDCLanding from '@/content/locales/en/dc/landing.json'

// English - CO
import enCOCommon from '@/content/locales/en/co/common.json'
import enCOLanding from '@/content/locales/en/co/landing.json'

// Spanish - DC
import esDCCommon from '@/content/locales/es/dc/common.json'
import esDCLanding from '@/content/locales/es/dc/landing.json'

// Spanish - CO
import esCOCommon from '@/content/locales/es/co/common.json'
import esCOLanding from '@/content/locales/es/co/landing.json'

// Get state from environment
const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase() as
  | 'dc'
  | 'co'

// Map state to resources
const stateResources = {
  dc: {
    en: {
      common: enDCCommon,
      landing: enDCLanding
    },
    es: {
      common: esDCCommon,
      landing: esDCLanding
    }
  },
  co: {
    en: {
      common: enCOCommon,
      landing: enCOLanding
    },
    es: {
      common: esCOCommon,
      landing: esCOLanding
    }
  }
}

// Get resources for current state using explicit conditional for security
const resources = state === 'co' ? stateResources.co : stateResources.dc

// State name mapping for interpolation
const stateNames = {
  dc: 'District of Columbia',
  co: 'Colorado'
} as const

// Supported languages
export const supportedLanguages = ['en', 'es', 'am'] as const
export type SupportedLanguage = (typeof supportedLanguages)[number]

// Initialize i18next with 'en' to ensure SSR/client consistency
// Language sync from localStorage happens in I18nProvider after hydration
i18n.use(initReactI18next).init({
  resources,
  lng: 'en',
  fallbackLng: 'en',
  defaultNS: 'common',
  ns: ['common', 'landing'],

  interpolation: {
    escapeValue: false, // React already escapes values
    // Default values for common interpolation variables
    defaultVariables: {
      state: state.toUpperCase(),
      stateName: state === 'co' ? stateNames.co : stateNames.dc,
      year: new Date().getFullYear().toString()
    }
  },

  react: {
    useSuspense: false // Disable suspense for SSR compatibility
  },

  // Debug mode in development
  debug: process.env.NODE_ENV === 'development'
})

export default i18n

// Export helpers
export const languageNames: Record<SupportedLanguage, string> = {
  en: 'English',
  es: 'Español',
  am: 'አማርኛ'
}

/**
 * Change the current language
 * Also persists to localStorage and updates document lang attribute
 */
export function changeLanguage(lng: SupportedLanguage): void {
  i18n.changeLanguage(lng)
  if (typeof window !== 'undefined') {
    localStorage.setItem('i18nextLng', lng)
    document.documentElement.lang = lng
  }
}

/**
 * Get the current language
 */
export function getCurrentLanguage(): SupportedLanguage {
  return (i18n.language as SupportedLanguage) || 'en'
}
