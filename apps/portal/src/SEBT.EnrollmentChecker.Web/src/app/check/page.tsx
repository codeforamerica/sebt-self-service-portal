'use client'

// apiBaseUrl comes from NEXT_PUBLIC_API_BASE_URL — intentionally public.
// SSR mode: undefined (ChildFormPage calls relative /api/enrollment/* routes, this app's own proxy).
// SSG mode: portal URL (e.g. https://portal.example.gov) — requests go to the portal's catch-all proxy.
// BACKEND_URL (private) is never exposed to the client.
import { ChildFormPage } from '@/features/enrollment/components/ChildFormPage'
import { SubmissionGate } from '@/features/enrollment/components/SubmissionGate'
import { useEnrollmentSubmit } from '@/features/enrollment/hooks/useEnrollmentSubmit'
import { getFlowConfig } from '@/lib/flowConfig'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  const { showSchoolField, apiBaseUrl, portalUrl } = getEnrollmentConfig()
  const submission = useEnrollmentSubmit()
  const { useReviewStep } = getFlowConfig()

  return (
    <SubmissionGate
      submission={submission}
      portalUrl={portalUrl}
    >
      <ChildFormPage
        showSchoolField={showSchoolField}
        apiBaseUrl={apiBaseUrl}
        {...(!useReviewStep && { onSubmitChildren: submission.submit })}
      />
    </SubmissionGate>
  )
}
