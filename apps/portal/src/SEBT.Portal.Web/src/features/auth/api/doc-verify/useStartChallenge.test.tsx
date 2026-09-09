import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'

import { AuthProvider } from '../../context'
import { useStartChallenge } from './useStartChallenge'

import { server } from '@/mocks/server'

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

describe('useStartChallenge', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('returns docvTransactionToken and docvUrl on success', async () => {
    const { result } = renderHook(() => useStartChallenge(), {
      wrapper: createWrapper()
    })

    result.current.mutate('mock-challenge-123')

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data).toEqual({
      docvTransactionToken: 'mock-token-for-testing',
      docvUrl: 'https://websdk.socure.com'
    })
  })

  it('reports error on 404', async () => {
    server.use(
      http.get('/api/challenges/:id/start', () => {
        return HttpResponse.json({ error: 'Challenge not found' }, { status: 404 })
      })
    )

    const { result } = renderHook(() => useStartChallenge(), {
      wrapper: createWrapper()
    })

    result.current.mutate('invalid-challenge')

    await waitFor(() => {
      expect(result.current.isError).toBe(true)
    })
  })
})
