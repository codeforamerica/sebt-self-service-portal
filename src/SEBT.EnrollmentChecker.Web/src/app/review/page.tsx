'use client'

import { ReviewPage } from '@/features/enrollment/components/ReviewPage'
import { checkEnrollment } from '@/features/enrollment/api/checkEnrollment'
import { useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const { state } = useEnrollment()
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const config = getEnrollmentConfig()
  const { setPageData, trackEvent } = useDataLayer()

  async function handleSubmit() {
    if (isSubmitting) return
    setError(null)
    setIsSubmitting(true)
    try {
      const response = await checkEnrollment(state.children, config.apiBaseUrl)
      // Pass results via sessionStorage (avoids URL length limits and keeps data off URL)
      sessionStorage.setItem('enrollmentResults', JSON.stringify(response))
      router.push('/results')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error'
      const errorCode = message.includes('rate') ? 'RATE_LIMIT' : 'SUBMISSION_ERROR'
      setPageData('error_code', errorCode)
      trackEvent(AnalyticsEvents.ENROLLMENT_CHECK_ERROR)
      setError(message.includes('rate') ? t('rateLimitError') : t('submitError'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <>
      {error && <Alert variant="error">{error}</Alert>}
      <ReviewPage onSubmit={handleSubmit} />
    </>
  )
}
