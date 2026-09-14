'use client'

import { useTranslation } from 'react-i18next'

import { useCheckerFeatures } from '@/features/maintenance/hooks/useCheckerFeatures'
import { getApplyHref } from './applyHref'
import { getEnrollmentConfig } from './stateConfig'

/**
 * The state's apply-form URL, or null when applications are closed. Callers hide
 * their apply UI on null.
 *
 * Two things must agree: the `enable_apply` flag, which state ops can flip at
 * runtime via AWS AppConfig, and a configured destination. A missing flag or a
 * failed features fetch reads as closed — the safe direction once the
 * application window has ended.
 */
export function useApplyHref(): string | null {
  const { i18n } = useTranslation()
  const { apiBaseUrl } = getEnrollmentConfig()
  const { data } = useCheckerFeatures(apiBaseUrl)

  if (!data?.apply?.enabled) {
    return null
  }

  return getApplyHref(i18n.language)
}
