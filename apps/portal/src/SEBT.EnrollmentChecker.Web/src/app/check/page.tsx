// apiBaseUrl comes from NEXT_PUBLIC_API_BASE_URL — intentionally public.
// SSR mode: undefined (ChildFormPage calls relative /api/enrollment/* routes, this app's own proxy).
// SSG mode: portal URL (e.g. https://portal.example.gov) — requests go to the portal's catch-all proxy.
// BACKEND_URL (private) is never exposed to the client.
import { ChildFormPage } from '@/features/enrollment/components/ChildFormPage'
import { getEnrollmentConfig } from '@/lib/stateConfig'

export default function Page() {
  const { showSchoolField, apiBaseUrl } = getEnrollmentConfig()
  return <ChildFormPage showSchoolField={showSchoolField} apiBaseUrl={apiBaseUrl} />
}
