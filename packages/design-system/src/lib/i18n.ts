/**
 * i18n Configuration for SEBT Design System
 *
 * Uses react-i18next with state-specific locale namespaces.
 * Translations are loaded statically for the current state (DC, CO).
 *
 * Key Features:
 * - State-specific translations: Each state has its own copy
 * - Namespace-based: Translations split by page/component
 * - Language detection: Browser preference with fallback to English
 * - Runtime interpolation: Supports {state}, {year}, {stateName} variables
 *
 * Directory Structure:
 *   content/locales/{locale}/{state}/{namespace}.json
 *   e.g., content/locales/en/dc/landing.json
 *
 * Bundle Optimization Note:
 * Static imports are used instead of dynamic loading because:
 * 1. SSR requires synchronous access to translations
 * 2. Total locale size (~50-60KB compressed per state) is acceptable
 * 3. Build-time state isolation ensures only one state's translations are bundled
 *
 * Import Management:
 * All locale imports are auto-generated in generated-locale-resources.ts
 * by content/scripts/generate-locales.js. Adding a new namespace JSON file
 * only requires running `pnpm copy:generate` — no manual import edits needed.
 */

import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// Supported languages
export const supportedLanguages = ['en', 'es', 'am'] as const
export type SupportedLanguage = (typeof supportedLanguages)[number]

/** Map of state code → i18next resource bundle (language → namespace → key → value) */
export type StateResources = Record<
  string,
  Record<string, Record<string, Record<string, string>>> | undefined
>

/**
 * Initialize i18next for SEBT apps.
 * Call this once at app startup, passing the generated locale resources.
 *
 * @param stateResources  - imported from the app's generated-locale-resources.ts
 * @param namespaces      - imported from the app's generated-locale-resources.ts
 * @param state           - current state code (e.g. 'co', 'dc')
 */
export function initI18n(
  stateResources: StateResources,
  namespaces: readonly string[],
  state: string
): void {
  const stateNames: Record<string, string> = {
    dc: 'District of Columbia',
    co: 'Colorado'
  }

  // eslint-disable-next-line security/detect-object-injection -- state is validated at build time
  const resources = stateResources[state] ?? stateResources['dc'] ?? {}

  i18n.use(initReactI18next).init({
    resources,
    lng: 'en',
    fallbackLng: 'en',
    defaultNS: 'common',
    ns: [...namespaces],
    interpolation: {
      escapeValue: false,
      defaultVariables: {
        state: state.toUpperCase(),
        // eslint-disable-next-line security/detect-object-injection -- state is validated at build time
        stateName: stateNames[state] ?? stateNames['dc'],
        year: new Date().getFullYear().toString()
      }
    },
    react: { useSuspense: false },
    debug: process.env.NODE_ENV === 'development'
  })
}

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
