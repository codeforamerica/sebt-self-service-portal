import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import {
  SubmitIdProofingResponseSchema,
  type SubmitIdProofingRequest,
  type SubmitIdProofingResponse
} from './schema'

// TODO: Replace with actual endpoint path once confirmed with backend team.
// The confirmed path should NOT start with '/auth/' — apiFetch auto-clears the
// auth token and redirects to /login on 401 for any '/auth/' endpoint (see
// client.ts). For id-proofing, a 401 means "not authorized for this resource,"
// not "session expired," so it must live outside that prefix.
const ID_PROOFING_ENDPOINT = '/id-proofing'

async function submitIdProofing(data: SubmitIdProofingRequest): Promise<SubmitIdProofingResponse> {
  const response = await apiFetch<SubmitIdProofingResponse>(ID_PROOFING_ENDPOINT, {
    method: 'POST',
    body: data,
    schema: SubmitIdProofingResponseSchema
  })

  // apiFetch returns undefined for 204/201 responses, bypassing schema validation.
  // This endpoint's contract requires a JSON body — guard against a backend mismatch.
  if (!response) {
    throw new ApiError('Expected a JSON response from id-proofing endpoint', 422)
  }

  return response
}

export function useSubmitIdProofing() {
  return useMutation({
    mutationFn: submitIdProofing,
    retry: (failureCount, error) => {
      // Don't retry client errors (4xx) — these are validation/auth issues
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      // Retry server errors (5xx) up to 2 times
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
