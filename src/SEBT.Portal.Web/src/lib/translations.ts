/**
 * Server-side translations for SEBT Portal
 *
 * This module provides translation utilities for Server Components.
 * For Client Components, use react-i18next via the I18nProvider.
 */

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

type Namespace =
  | 'common'
  | 'confirmInfo'
  | 'dashboard'
  | 'disclaimer'
  | 'editContactPreferences'
  | 'editMailingAddress'
  | 'idProofing'
  | 'landing'
  | 'login'
  | 'offBoarding'
  | 'optIn'
  | 'optionalId'
  | 'personalInfo'
  | 'proto'
  | 'result'

// Get state from environment
const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase() as
  | 'dc'
  | 'co'

// Map state to resources (English only for SSR - language switching happens client-side)
const stateResources: Record<'dc' | 'co', Record<Namespace, Record<string, string>>> = {
  dc: {
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
    result: enDCResult
  },
  co: {
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
    result: enCOResult
  }
}

const resources = state === 'co' ? stateResources.co : stateResources.dc

/**
 * Get a translation function for a specific namespace
 * For use in Server Components
 */
export function getTranslations(namespace: Namespace) {
  // eslint-disable-next-line security/detect-object-injection -- namespace is typed Namespace
  const translations = resources[namespace]
  return (key: string): string => {
    // eslint-disable-next-line security/detect-object-injection -- key is from trusted translation files
    return translations[key] ?? key
  }
}
