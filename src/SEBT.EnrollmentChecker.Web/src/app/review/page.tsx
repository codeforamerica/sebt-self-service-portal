'use client'

import { ReviewPage } from '@/features/enrollment/components/ReviewPage'
import { checkEnrollment } from '@/features/enrollment/api/checkEnrollment'
import { getSubmitErrorMessage, type SubmitErrorKind } from '@/features/enrollment/copy/submitErrorCopy'
import { useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, LoadingInterstitial } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  // confirmInfo's t() is temporarily unused: submit-error copy is hardcoded in
  // submitErrorCopy.ts (DC-519). Keep i18n here to resolve the active language.
  const { i18n } = useTranslation('confirmInfo')
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const router = useRouter()
  const { state } = useEnrollment()
  const [errorKind, setErrorKind] = useState<SubmitErrorKind | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  // Synchronous re-entrancy guard. `isSubmitting` state can't catch a fast
  // double-click: both handlers run against the same render with the stale value
  // `false`, so both pass the guard before React re-renders. A ref is read/written
  // synchronously, so the second click sees the flag the first click just set.
  const submittingRef = useRef(false)
  const config = getEnrollmentConfig()
  const { setPageData, trackEvent } = useDataLayer()

  async function handleSubmit() {
    if (submittingRef.current) return
    submittingRef.current = true
    setErrorKind(null)
    setIsSubmitting(true)
    try {
      const response = await checkEnrollment(state.children, config.apiBaseUrl)
      // Pass results via sessionStorage (avoids URL length limits and keeps data off URL)
      sessionStorage.setItem('enrollmentResults', JSON.stringify(response))
      router.push('/results')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error'
      const isRateLimit = message.includes('rate')
      setPageData('error_code', isRateLimit ? 'RATE_LIMIT' : 'SUBMISSION_ERROR')
      trackEvent(AnalyticsEvents.ENROLLMENT_CHECK_ERROR)
      setErrorKind(isRateLimit ? 'rateLimit' : 'maintenance')
    } finally {
      submittingRef.current = false
      setIsSubmitting(false)
    }
  }

  if (isSubmitting) {
    return (
      <div className="grid-container maxw-tablet">
        <LoadingInterstitial
          title={tProcessing('title', 'Please wait...')}
          message={tProcessing(
            'body',
            'Do not exit the page. Checking to see if we have enough information.'
          )}
        />
      </div>
    )
  }

  return (
    <>
      {errorKind && <Alert variant="error">{getSubmitErrorMessage(errorKind, i18n.language)}</Alert>}
      <ReviewPage onSubmit={handleSubmit} isSubmitting={isSubmitting} />
    </>
  )
}
