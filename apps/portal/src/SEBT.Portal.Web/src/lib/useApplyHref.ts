'use client'

import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'

import { getApplyHref } from './applyHref'

/**
 * Returns the state's apply-form URL, or null when applications are closed.
 * Callers must hide their apply UI on null.
 *
 * Closure is driven by the `enable_apply` feature flag so state ops can cut
 * applications off (or reopen them) at runtime via AWS AppConfig without a
 * deploy. A missing flag, a failed flags fetch, or rendering outside the
 * feature-flags provider all read as closed — the safe direction once the
 * application window has ended.
 */
export function useApplyHref(): string | null {
  const { i18n } = useTranslation()
  const applyOpen = useFeatureFlag('enable_apply')

  if (!applyOpen) {
    return null
  }

  return getApplyHref(i18n.language)
}
