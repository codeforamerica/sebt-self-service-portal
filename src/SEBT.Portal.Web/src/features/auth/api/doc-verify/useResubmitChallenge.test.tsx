import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'

import { server } from '@/mocks/server'

import { AuthProvider } from '../../context'
import { useResubmitChallenge } from './useResubmitChallenge'

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

function createWrapper() {
  const queryClient = createTestQueryClient()
  function TestWrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    )
  }
  return TestWrapper
}

describe('useResubmitChallenge', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('returns the new challengeId, docvUrl, and token on success', async () => {
    const newId = '11111111-1111-4111-8111-111111111111'
    server.use(
      http.post('/api/challenges/:id/resubmit', () => {
        return HttpResponse.json({
          challengeId: newId,
          docvTransactionToken: 'fresh-token',
          docvUrl: 'https://verify.socure.com/#/dv/fresh-token'
        })
      })
    )

    const { result } = renderHook(() => useResubmitChallenge(), {
      wrapper: createWrapper()
    })

    result.current.mutate('22222222-2222-4222-8222-222222222222')

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data).toEqual({
      challengeId: newId,
      docvTransactionToken: 'fresh-token',
      docvUrl: 'https://verify.socure.com/#/dv/fresh-token'
    })
  })

  it('reports error on 409 (prior challenge not in Resubmit state)', async () => {
    server.use(
      http.post('/api/challenges/:id/resubmit', () => {
        return HttpResponse.json(
          { error: 'Challenge is in Pending state and cannot be resubmitted.' },
          { status: 409 }
        )
      })
    )

    const { result } = renderHook(() => useResubmitChallenge(), {
      wrapper: createWrapper()
    })

    result.current.mutate('any-id')

    await waitFor(() => {
      expect(result.current.isError).toBe(true)
    })
  })
})
