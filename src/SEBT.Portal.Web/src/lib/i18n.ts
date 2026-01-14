/**
 * i18n Configuration for SEBT Portal
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
 */

import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// Import locale files statically for SSR support
// English - DC
import enDCCommon from '@/content/locales/en/dc/common.json'
import enDCConfirmInfo from '@/content/locales/en/dc/confirmInfo.json'
import enDCDashboard from '@/content/locales/en/dc/dashboard.json'
import enDCDisclaimer from '@/content/locales/en/dc/disclaimer.json'
import enDCEditContactPreferences from '@/content/locales/en/dc/editContactPreferences.json'
import enDCEditMailingAddress from '@/content/locales/en/dc/editMailingAddress.json'
import enDCIdProofing from '@/content/locales/en/dc/idProofing.json'
import enDCLanding from '@/content/locales/en/dc/landing.json'
import enDCLogin from '@/content/locales/en/dc/login.json'
import enDCOffBoarding from '@/content/locales/en/dc/offBoarding.json'
import enDCOptIn from '@/content/locales/en/dc/optIn.json'
import enDCOptionalId from '@/content/locales/en/dc/optionalId.json'
import enDCPersonalInfo from '@/content/locales/en/dc/personalInfo.json'
import enDCProto from '@/content/locales/en/dc/proto.json'
import enDCResult from '@/content/locales/en/dc/result.json'
import enDCValidation from '@/content/locales/en/dc/validation.json'

// English - CO
import enCOCommon from '@/content/locales/en/co/common.json'
import enCOConfirmInfo from '@/content/locales/en/co/confirmInfo.json'
import enCODashboard from '@/content/locales/en/co/dashboard.json'
import enCODisclaimer from '@/content/locales/en/co/disclaimer.json'
import enCOEditContactPreferences from '@/content/locales/en/co/editContactPreferences.json'
import enCOEditMailingAddress from '@/content/locales/en/co/editMailingAddress.json'
import enCOIdProofing from '@/content/locales/en/co/idProofing.json'
import enCOLanding from '@/content/locales/en/co/landing.json'
import enCOLogin from '@/content/locales/en/co/login.json'
import enCOOffBoarding from '@/content/locales/en/co/offBoarding.json'
import enCOOptIn from '@/content/locales/en/co/optIn.json'
import enCOOptionalId from '@/content/locales/en/co/optionalId.json'
import enCOPersonalInfo from '@/content/locales/en/co/personalInfo.json'
import enCOProto from '@/content/locales/en/co/proto.json'
import enCOResult from '@/content/locales/en/co/result.json'
import enCOValidation from '@/content/locales/en/co/validation.json'

// Spanish - DC
import esDCCommon from '@/content/locales/es/dc/common.json'
import esDCConfirmInfo from '@/content/locales/es/dc/confirmInfo.json'
import esDCDashboard from '@/content/locales/es/dc/dashboard.json'
import esDCDisclaimer from '@/content/locales/es/dc/disclaimer.json'
import esDCEditContactPreferences from '@/content/locales/es/dc/editContactPreferences.json'
import esDCEditMailingAddress from '@/content/locales/es/dc/editMailingAddress.json'
import esDCIdProofing from '@/content/locales/es/dc/idProofing.json'
import esDCLanding from '@/content/locales/es/dc/landing.json'
import esDCLogin from '@/content/locales/es/dc/login.json'
import esDCOffBoarding from '@/content/locales/es/dc/offBoarding.json'
import esDCOptIn from '@/content/locales/es/dc/optIn.json'
import esDCOptionalId from '@/content/locales/es/dc/optionalId.json'
import esDCPersonalInfo from '@/content/locales/es/dc/personalInfo.json'
import esDCProto from '@/content/locales/es/dc/proto.json'
import esDCResult from '@/content/locales/es/dc/result.json'
import esDCValidation from '@/content/locales/es/dc/validation.json'

// Spanish - CO
import esCOCommon from '@/content/locales/es/co/common.json'
import esCOConfirmInfo from '@/content/locales/es/co/confirmInfo.json'
import esCODashboard from '@/content/locales/es/co/dashboard.json'
import esCODisclaimer from '@/content/locales/es/co/disclaimer.json'
import esCOEditContactPreferences from '@/content/locales/es/co/editContactPreferences.json'
import esCOEditMailingAddress from '@/content/locales/es/co/editMailingAddress.json'
import esCOIdProofing from '@/content/locales/es/co/idProofing.json'
import esCOLanding from '@/content/locales/es/co/landing.json'
import esCOLogin from '@/content/locales/es/co/login.json'
import esCOOffBoarding from '@/content/locales/es/co/offBoarding.json'
import esCOOptIn from '@/content/locales/es/co/optIn.json'
import esCOOptionalId from '@/content/locales/es/co/optionalId.json'
import esCOPersonalInfo from '@/content/locales/es/co/personalInfo.json'
import esCOProto from '@/content/locales/es/co/proto.json'
import esCOResult from '@/content/locales/es/co/result.json'
import esCOValidation from '@/content/locales/es/co/validation.json'

// Get state from environment
const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase() as
  | 'dc'
  | 'co'

// Map state to resources
const stateResources = {
  dc: {
    en: {
      common: enDCCommon,
      confirmInfo: enDCConfirmInfo,
      dashboard: enDCDashboard,
      disclaimer: enDCDisclaimer,
      editContactPreferences: enDCEditContactPreferences,
      editMailingAddress: enDCEditMailingAddress,
      idProofing: enDCIdProofing,
      landing: enDCLanding,
      login: enDCLogin,
      offBoarding: enDCOffBoarding,
      optIn: enDCOptIn,
      optionalId: enDCOptionalId,
      personalInfo: enDCPersonalInfo,
      proto: enDCProto,
      result: enDCResult,
      validation: enDCValidation
    },
    es: {
      common: esDCCommon,
      confirmInfo: esDCConfirmInfo,
      dashboard: esDCDashboard,
      disclaimer: esDCDisclaimer,
      editContactPreferences: esDCEditContactPreferences,
      editMailingAddress: esDCEditMailingAddress,
      idProofing: esDCIdProofing,
      landing: esDCLanding,
      login: esDCLogin,
      offBoarding: esDCOffBoarding,
      optIn: esDCOptIn,
      optionalId: esDCOptionalId,
      personalInfo: esDCPersonalInfo,
      proto: esDCProto,
      result: esDCResult,
      validation: esDCValidation
    }
  },
  co: {
    en: {
      common: enCOCommon,
      confirmInfo: enCOConfirmInfo,
      dashboard: enCODashboard,
      disclaimer: enCODisclaimer,
      editContactPreferences: enCOEditContactPreferences,
      editMailingAddress: enCOEditMailingAddress,
      idProofing: enCOIdProofing,
      landing: enCOLanding,
      login: enCOLogin,
      offBoarding: enCOOffBoarding,
      optIn: enCOOptIn,
      optionalId: enCOOptionalId,
      personalInfo: enCOPersonalInfo,
      proto: enCOProto,
      result: enCOResult,
      validation: enCOValidation
    },
    es: {
      common: esCOCommon,
      confirmInfo: esCOConfirmInfo,
      dashboard: esCODashboard,
      disclaimer: esCODisclaimer,
      editContactPreferences: esCOEditContactPreferences,
      editMailingAddress: esCOEditMailingAddress,
      idProofing: esCOIdProofing,
      landing: esCOLanding,
      login: esCOLogin,
      offBoarding: esCOOffBoarding,
      optIn: esCOOptIn,
      optionalId: esCOOptionalId,
      personalInfo: esCOPersonalInfo,
      proto: esCOProto,
      result: esCOResult,
      validation: esCOValidation
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
  ns: [
    'common',
    'confirmInfo',
    'dashboard',
    'disclaimer',
    'editContactPreferences',
    'editMailingAddress',
    'idProofing',
    'landing',
    'login',
    'offBoarding',
    'optIn',
    'optionalId',
    'personalInfo',
    'proto',
    'result',
    'validation'
  ],

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
