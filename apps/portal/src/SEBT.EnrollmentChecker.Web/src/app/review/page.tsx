'use client'

import { ReviewPage } from '@/features/enrollment/components/ReviewPage'
import { SubmissionGate } from '@/features/enrollment/components/SubmissionGate'
import { useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { useEnrollmentSubmit } from '@/features/enrollment/hooks/useEnrollmentSubmit'
import { getFlowConfig } from '@/lib/flowConfig'
import { getEnrollmentConfig } from '@/lib/stateConfig'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

export default function Page() {
  const router = useRouter()
  const { state } = useEnrollment()
  const config = getEnrollmentConfig()
  const submission = useEnrollmentSubmit()
  const { useReviewStep } = getFlowConfig()

  // States without a review step submit straight from the form, so this route
  // is not part of their flow — a deep link or a stale bookmark lands back on
  // the form rather than on a screen their content has no copy for.
  useEffect(() => {
    if (!useReviewStep) {
      router.replace('/check')
    }
  }, [useReviewStep, router])

  if (!useReviewStep) {
    return null
  }

  return (
    <SubmissionGate
      submission={submission}
      portalUrl={config.portalUrl}
    >
      <ReviewPage onSubmit={() => void submission.submit(state.children)} />
    </SubmissionGate>
  )
}
