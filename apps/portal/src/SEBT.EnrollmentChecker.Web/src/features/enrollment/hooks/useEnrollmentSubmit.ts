'use client'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { useRouter } from 'next/navigation'
import { useRef, useState } from 'react'

import { getEnrollmentConfig } from '@/lib/stateConfig'
import { checkEnrollment } from '../api/checkEnrollment'
import type { Child } from '../context/EnrollmentContext'

export type SubmitErrorKind = 'rateLimit' | 'generic'

export interface EnrollmentSubmit {
  submit: (children: Child[]) => Promise<void>
  isSubmitting: boolean
  errorKind: SubmitErrorKind | null
}

/**
 * Runs the enrollment check and navigates to the results.
 *
 * Lives in a hook because two screens start a check: the review step in states
 * that have one, and the child form directly in states that don't. Callers pass
 * the children explicitly so a form can submit a record it just created, before
 * the context state update lands.
 */
export function useEnrollmentSubmit(): EnrollmentSubmit {
  const router = useRouter()
  const config = getEnrollmentConfig()
  const { setPageData, trackEvent } = useDataLayer()
  const [errorKind, setErrorKind] = useState<SubmitErrorKind | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  // Synchronous re-entrancy guard. `isSubmitting` state can't catch a fast
  // double-click: both handlers run against the same render with the stale value
  // `false`, so both pass the guard before React re-renders. A ref is read/written
  // synchronously, so the second click sees the flag the first click just set.
  const submittingRef = useRef(false)

  async function submit(children: Child[]) {
    if (submittingRef.current) return
    submittingRef.current = true
    setErrorKind(null)
    setIsSubmitting(true)
    try {
      if (window.fbq && process.env.NEXT_PUBLIC_META_PIXEL && process.env.NEXT_PUBLIC_META_PIXEL_ACTION) {
        window.fbq('trackSingleCustom', process.env.NEXT_PUBLIC_META_PIXEL, process.env.NEXT_PUBLIC_META_PIXEL_ACTION)
      }
      const response = await checkEnrollment(children, config.apiBaseUrl)
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

  return { submit, isSubmitting, errorKind }
}
