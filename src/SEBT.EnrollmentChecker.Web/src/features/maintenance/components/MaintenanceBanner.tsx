'use client'

import { getEnrollmentConfig } from '@/lib/stateConfig'
import { Alert } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useCheckerFeatures } from '../hooks/useCheckerFeatures'
import { resolveMaintenanceMessage } from '../resolveMaintenanceMessage'

export function MaintenanceBanner() {
  // useTranslation subscribes this component to language changes, so the banner
  // re-resolves its configured copy when the user switches languages.
  const { i18n } = useTranslation()
  const { apiBaseUrl } = getEnrollmentConfig()
  const { data, error, isStale } = useCheckerFeatures(apiBaseUrl)

  useEffect(() => {
    if (!error) {
      return
    }
    if (data && !isStale) {
      // A poll failed but the last successful state is recent enough to trust;
      // keep showing it so a transient blip doesn't blank a real notice.
      console.warn('Features poll failed; showing last-known maintenance banner state.', error)
    } else {
      // Fail closed (no banner): nothing trustworthy to show, either because no
      // fetch has succeeded yet or failures outlasted the staleness tolerance.
      console.warn('Maintenance banner state unavailable or stale; hiding banner.', error)
    }
  }, [error, data, isStale])

  if (!data || isStale) {
    return null
  }

  const message = resolveMaintenanceMessage(
    data.maintenanceBanner.enabled,
    data.maintenanceBanner.message,
    i18n.language
  )

  if (!message) {
    return null
  }

  return (
    <Alert
      variant="warning"
      className="margin-top-0"
    >
      {message}
    </Alert>
  )
}
