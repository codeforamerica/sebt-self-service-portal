import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../../mocks/server'
import { useSchools } from './useSchools'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('useSchools', () => {
  it('returns schools when enabled', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Elm School', code: 'ELM' }])
      )
    )
    const { result } = renderHook(() => useSchools({ enabled: true, apiBaseUrl: '' }), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.[0]?.name).toBe('Elm School')
  })

  it('does not fetch when disabled', () => {
    const { result } = renderHook(() => useSchools({ enabled: false, apiBaseUrl: '' }), { wrapper })
    expect(result.current.isFetching).toBe(false)
  })

  it('enters error state on network failure', async () => {
    server.use(
      http.get('/api/enrollment/schools', () => new HttpResponse(null, { status: 500 }))
    )
    const { result } = renderHook(
      () => useSchools({ enabled: true, apiBaseUrl: '' }),
      { wrapper }
    )
    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
