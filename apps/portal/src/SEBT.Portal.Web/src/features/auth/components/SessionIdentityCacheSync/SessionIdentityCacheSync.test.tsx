import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'

import { householdDataQueryKey } from '@/features/household/api/queryKeys'
import { server } from '@/mocks/server'

import { AuthProvider, useAuth } from '../../context'
import { SessionIdentityCacheSync } from './SessionIdentityCacheSync'

const USER_A_ID = '018f0000-0000-7000-8000-000000000001'
const USER_B_ID = '018f0000-0000-7000-8000-000000000002'

function TestHarness() {
  const { session, login } = useAuth()

  return (
    <>
      <SessionIdentityCacheSync />
      <span data-testid="user-id">{session?.userId ?? 'none'}</span>
      <button
        type="button"
        onClick={() => {
          void login()
        }}
      >
        Refresh session
      </button>
    </>
  )
}

describe('SessionIdentityCacheSync', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('clears household cache when portal userId changes', async () => {
    let statusCallCount = 0
    server.use(
      http.get('/api/auth/status', () => {
        statusCallCount += 1
        if (statusCallCount === 1) {
          return HttpResponse.json({
            isAuthorized: true,
            userId: USER_A_ID,
            email: 'user-a@example.com',
            ial: '1'
          })
        }
        return HttpResponse.json({
          isAuthorized: true,
          userId: USER_B_ID,
          email: 'user-b@example.com',
          ial: '1'
        })
      })
    )

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } }
    })
    queryClient.setQueryData(householdDataQueryKey(USER_A_ID), { email: 'user-a@example.com' })

    const user = userEvent.setup()

    render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <TestHarness />
        </AuthProvider>
      </QueryClientProvider>
    )

    await waitFor(() => {
      expect(screen.getByTestId('user-id')).toHaveTextContent(USER_A_ID)
    })
    expect(queryClient.getQueryData(householdDataQueryKey(USER_A_ID))).toBeDefined()

    await act(async () => {
      await user.click(screen.getByRole('button', { name: /refresh session/i }))
    })

    await waitFor(() => {
      expect(screen.getByTestId('user-id')).toHaveTextContent(USER_B_ID)
    })
    expect(queryClient.getQueryData(householdDataQueryKey(USER_A_ID))).toBeUndefined()
  })

  it('clears household cache when the user logs out', async () => {
    let statusCallCount = 0
    server.use(
      http.get('/api/auth/status', () => {
        statusCallCount += 1
        if (statusCallCount === 1) {
          return HttpResponse.json({
            isAuthorized: true,
            userId: USER_A_ID,
            email: 'user-a@example.com',
            ial: '1'
          })
        }
        return HttpResponse.json({ isAuthorized: false }, { status: 401 })
      })
    )

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } }
    })
    queryClient.setQueryData(householdDataQueryKey(USER_A_ID), { email: 'user-a@example.com' })

    const user = userEvent.setup()

    render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <TestHarness />
        </AuthProvider>
      </QueryClientProvider>
    )

    await waitFor(() => {
      expect(screen.getByTestId('user-id')).toHaveTextContent(USER_A_ID)
    })
    expect(queryClient.getQueryData(householdDataQueryKey(USER_A_ID))).toBeDefined()

    await act(async () => {
      await user.click(screen.getByRole('button', { name: /refresh session/i }))
    })

    await waitFor(() => {
      expect(screen.getByTestId('user-id')).toHaveTextContent('none')
    })
    expect(queryClient.getQueryData(householdDataQueryKey(USER_A_ID))).toBeUndefined()
  })
})
