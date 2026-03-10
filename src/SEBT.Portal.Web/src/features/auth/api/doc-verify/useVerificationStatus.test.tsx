import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'

import { AuthProvider } from '../../context'
import { useVerificationStatus } from './useVerificationStatus'

import { server } from '@/mocks/server'

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false }
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

describe('useVerificationStatus', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('does not fetch when challengeId is undefined', () => {
    const { result } = renderHook(() => useVerificationStatus(undefined), {
      wrapper: createWrapper()
    })

    expect(result.current.isFetching).toBe(false)
  })

  it('returns pending status on first poll', async () => {
    // Override to always return pending for this test
    server.use(
      http.get('/api/id-proofing/status', () => {
        return HttpResponse.json({ status: 'pending' })
      })
    )

    const { result } = renderHook(() => useVerificationStatus('challenge-123'), {
      wrapper: createWrapper()
    })

    await waitFor(() => {
      expect(result.current.data?.status).toBe('pending')
    })
  })

  it('returns verified status', async () => {
    server.use(
      http.get('/api/id-proofing/status', () => {
        return HttpResponse.json({ status: 'verified' })
      })
    )

    const { result } = renderHook(() => useVerificationStatus('challenge-123'), {
      wrapper: createWrapper()
    })

    await waitFor(() => {
      expect(result.current.data?.status).toBe('verified')
    })
  })

  it('returns rejected status with offboardingReason', async () => {
    server.use(
      http.get('/api/id-proofing/status', () => {
        return HttpResponse.json({
          status: 'rejected',
          offboardingReason: 'docVerificationFailed'
        })
      })
    )

    const { result } = renderHook(() => useVerificationStatus('challenge-123'), {
      wrapper: createWrapper()
    })

    await waitFor(() => {
      expect(result.current.data?.status).toBe('rejected')
      expect(result.current.data?.offboardingReason).toBe('docVerificationFailed')
    })
  })

  it('reports error on 401', async () => {
    server.use(
      http.get('/api/id-proofing/status', () => {
        return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
      })
    )

    const { result } = renderHook(() => useVerificationStatus('challenge-123'), {
      wrapper: createWrapper()
    })

    await waitFor(() => {
      expect(result.current.isError).toBe(true)
    })
  })
})
