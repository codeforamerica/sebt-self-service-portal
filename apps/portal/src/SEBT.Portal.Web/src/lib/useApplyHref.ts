'use client'

import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'
import { useRuntimeConfig } from '@/providers'

import { getApplyHref } from './applyHref'

/**
 * Returns the apply-form URL, or null when applications are closed or have no
 * destination. Callers must hide their apply UI on null.
 *
 * Closure is driven by the `enable_apply` feature flag so state ops can cut
 * applications off (or reopen them) at runtime via AWS AppConfig without a
 * deploy. A missing flag, a failed flags fetch, or rendering outside the
 * feature-flags provider all read as closed — the safe direction once the
 * application window has ended.
 *
 * The destination comes from runtime config (`APPLICATION_URL`). Open
 * applications with no URL configured is a misconfiguration: the apply UI
 * stays hidden and the hook warns once.
 */
export function useApplyHref(): string | null {
  const { i18n } = useTranslation()
  const applyOpen = useFeatureFlag('enable_apply')
  const { applicationUrl, applicationUrlPeakParams } = useRuntimeConfig()

  const missingUrl = applyOpen && !applicationUrl

  useEffect(() => {
    if (missingUrl) {
      console.warn('enable_apply is on but APPLICATION_URL is not set; hiding the apply UI.')
    }
  }, [missingUrl])

  if (!applyOpen) {
    return null
  }

  return getApplyHref(i18n.language, { applicationUrl, peakParams: applicationUrlPeakParams })
}
