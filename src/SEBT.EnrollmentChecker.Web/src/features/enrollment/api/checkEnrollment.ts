import type { Child } from '../context/EnrollmentContext'
import {
  enrollmentCheckResponseSchema,
  type EnrollmentCheckResponse
} from '../schemas/enrollmentSchema'

/**
 * POST /api/enrollment/check
 *
 * @param children - children to check, from EnrollmentContext
 * @param apiBaseUrl - SSG: portal Node server URL (NEXT_PUBLIC_API_BASE_URL).
 *                    SSR: '' (same-origin /api route handles it).
 */
export async function checkEnrollment(
  children: Child[],
  apiBaseUrl: string
): Promise<EnrollmentCheckResponse> {
  const url = `${apiBaseUrl}/api/enrollment/check`

  const body = {
    children: children.map(child => {
      const additionalFields: Record<string, string> = {}
      if (child.middleName) {
        additionalFields['MiddleName'] = child.middleName
      }

      return {
        firstName: child.firstName,
        lastName: child.lastName,
        dateOfBirth: child.dateOfBirth,
        ...(child.schoolName ? { schoolName: child.schoolName } : {}),
        ...(child.schoolCode ? { schoolCode: child.schoolCode } : {}),
        ...(Object.keys(additionalFields).length > 0 ? { additionalFields } : {})
      }
    })
  }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (response.status === 429) {
    throw new Error('rate limit exceeded — please wait before trying again')
  }

  if (!response.ok) {
    throw new Error(`enrollment check failed: ${response.status.toString()}`)
  }

  const data: unknown = await response.json()
  return enrollmentCheckResponseSchema.parse(data)
}
