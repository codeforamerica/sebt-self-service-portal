'use client'

import { ResultsPage } from '@/features/enrollment/components/ResultsPage'
import { enrollmentCheckResponseSchema } from '@/features/enrollment/schemas/enrollmentSchema'
import { getEnrollmentConfig } from '@/lib/stateConfig'
import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import type { EnrollmentCheckResponse } from '@/features/enrollment/schemas/enrollmentSchema'

/** Maps API status to analytics match type per the data layer dictionary. */
function deriveMatchType(results: EnrollmentCheckResponse['results']): string {
  const hasMatch = results.some(r => r.status === 'Match')
  const hasPossible = results.some(r => r.status === 'PossibleMatch')
  if (hasMatch) return 'exact'
  if (hasPossible) return 'fuzzy'
  return 'none'
}

export default function Page() {
  const router = useRouter()
  const config = getEnrollmentConfig()
  const [response, setResponse] = useState<EnrollmentCheckResponse | null>(null)
  const { setPageData, setUserData, trackEvent } = useDataLayer()
  const tracked = useRef(false)

  useEffect(() => {
    const raw = sessionStorage.getItem('enrollmentResults')
    if (!raw) { router.replace('/'); return }
    try {
      const parsed = enrollmentCheckResponseSchema.parse(JSON.parse(raw))
      setResponse(parsed)

      // Track analytics inline — avoids a second useEffect reacting to state change
      if (!tracked.current) {
        tracked.current = true
        const matchType = deriveMatchType(parsed.results)
        const hasEligible = parsed.results.some(r => r.status === 'Match' || r.status === 'PossibleMatch')
        setPageData('enrollment_match_type', matchType)
        setUserData('sebt_eligible', hasEligible, ['default', 'analytics'])
        trackEvent(AnalyticsEvents.ENROLLMENT_CHECK_RESULT)
      }
    } catch {
      router.replace('/')
    }
  }, [router, setPageData, setUserData, trackEvent])

  if (!response) return null

  return <ResultsPage results={response.results} applicationUrl={config.applicationUrl} />
}
