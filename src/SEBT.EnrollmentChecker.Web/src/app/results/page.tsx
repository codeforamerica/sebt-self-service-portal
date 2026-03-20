'use client'

import { ResultsPage } from '@/features/enrollment/components/ResultsPage'
import { enrollmentCheckResponseSchema } from '@/features/enrollment/schemas/enrollmentSchema'
import { getEnrollmentConfig } from '@/lib/stateConfig'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import type { EnrollmentCheckResponse } from '@/features/enrollment/schemas/enrollmentSchema'

export default function Page() {
  const router = useRouter()
  const config = getEnrollmentConfig()
  const [response, setResponse] = useState<EnrollmentCheckResponse | null>(null)

  useEffect(() => {
    const raw = sessionStorage.getItem('enrollmentResults')
    if (!raw) { router.replace('/'); return }
    try {
      setResponse(enrollmentCheckResponseSchema.parse(JSON.parse(raw)))
    } catch {
      router.replace('/')
    }
  }, [router])

  if (!response) return null

  return <ResultsPage results={response.results} applicationUrl={config.applicationUrl} />
}
