'use client'

import { ErrorResultPage } from '@/features/enrollment/components/ErrorResultPage'
import { ReviewPage } from '@/features/enrollment/components/ReviewPage'
import { checkEnrollment } from '@/features/enrollment/api/checkEnrollment'
import { getRateLimitErrorMessage } from '@/features/enrollment/copy/submitErrorCopy'
import { useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { getClientConfig } from '@/lib/client-config'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, LoadingInterstitial } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getEnrollmentConfig } from '@/lib/stateConfig'

// The rate-limit message stays hardcoded until the content sheet grows a row for it.
type SubmitErrorKind = 'rateLimit' | 'generic'

export default function Page() {
  const { i18n } = useTranslation('dev')
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
      const { metaPixel, metaPixelAction } = getClientConfig()
      if (window.fbq && metaPixel && metaPixelAction) {
        window.fbq('trackSingleCustom', metaPixel, metaPixelAction)
      }
      const response = await checkEnrollment(state.children, config.apiBaseUrl)
      // Pass results via sessionStorage (avoids URL length limits and keeps data off URL)
      sessionStorage.setItem('enrollmentResults', JSON.stringify(response))
      router.push('/results')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error'
      const isRateLimit = message.includes('rate')
      setPageData('error_code', isRateLimit ? 'RATE_LIMIT' : 'SUBMISSION_ERROR')
      trackEvent(AnalyticsEvents.ENROLLMENT_CHECK_ERROR)
      setErrorKind(isRateLimit ? 'rateLimit' : 'generic')
    } finally {
      submittingRef.current = false
      setIsSubmitting(false)
    }
  }

  // The interstitial IS this flow's processing treatment: the whole review page
  // is replaced while the check runs, so ReviewPage carries no busy props.
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

  // A whole-check failure replaces the review form with the full error page
  // (portal next step, no application links — applications are closed, DC-701).
  // Rate limiting keeps the inline alert: the fix is simply waiting and
  // resubmitting the form that is already on screen.
  if (errorKind === 'generic') {
    return <ErrorResultPage portalUrl={config.portalUrl} />
  }

  return (
    <>
      {errorKind === 'rateLimit' && (
        <Alert variant="error">{getRateLimitErrorMessage(i18n.language)}</Alert>
      )}
      <ReviewPage onSubmit={handleSubmit} />
    </>
  )
}
