/**
 * AuthContext Unit Tests
 *
 * Tests the authentication context which sources its state from /auth/status
 * (backed by an HttpOnly session cookie) rather than client-accessible storage.
 */
import { act, render, renderHook, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'
import { http, HttpResponse } from 'msw'

import { AuthProvider, useAuth } from './AuthContext'

function mockAuthStatus(
  response: {
    status?: number
    body?: Record<string, unknown> | null
  } = {}
) {
  const { status = 200, body = { isAuthorized: true, email: 'user@example.com' } } = response
  server.use(
    http.get('/api/auth/status', () => {
      if (status === 401) return new HttpResponse(null, { status: 401 })
      return HttpResponse.json(body, { status })
    })
  )
}

describe('AuthContext', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  describe('AuthProvider', () => {
    it('starts loading and resolves to unauthenticated when /auth/status returns 401', async () => {
      mockAuthStatus({ status: 401 })

      const { result } = renderHook(() => useAuth(), {
        wrapper: AuthProvider
      })

      expect(result.current.isLoading).toBe(true)
      await waitFor(() => expect(result.current.isLoading).toBe(false))

      expect(result.current.session).toBeNull()
      expect(result.current.isAuthenticated).toBe(false)
    })

    it('populates session from /auth/status on mount when authorized', async () => {
      mockAuthStatus({
        body: {
          isAuthorized: true,
          email: 'user@example.com',
          ial: '1plus',
          idProofingStatus: 2,
          idProofingCompletedAt: 1735689600,
          idProofingExpiresAt: 1767225600
        }
      })

      const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider })

      await waitFor(() => expect(result.current.isLoading).toBe(false))

      expect(result.current.isAuthenticated).toBe(true)
      expect(result.current.session).toEqual({
        email: 'user@example.com',
        ial: '1plus',
        idProofingStatus: 2,
        idProofingCompletedAt: 1735689600,
        idProofingExpiresAt: 1767225600,
        isCoLoaded: null,
        expiresAt: null,
        absoluteExpiresAt: null
      })
    })

    it('login() re-fetches /auth/status and updates session', async () => {
      mockAuthStatus({ status: 401 })

      const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider })
      await waitFor(() => expect(result.current.isLoading).toBe(false))
      expect(result.current.isAuthenticated).toBe(false)

      mockAuthStatus({
        body: { isAuthorized: true, email: 'user@example.com', ial: '1' }
      })

      await act(async () => {
        await result.current.login()
      })

      expect(result.current.isAuthenticated).toBe(true)
      expect(result.current.session?.email).toBe('user@example.com')
    })
  })

  describe('useAuth', () => {
    it('throws when used outside AuthProvider', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      expect(() => {
        renderHook(() => useAuth())
      }).toThrow('useAuth must be used within an AuthProvider')

      consoleSpy.mockRestore()
    })
  })

  describe('Integration with components', () => {
    function TestComponent() {
      const { session, isAuthenticated, isLoading, login } = useAuth()

      if (isLoading) {
        return <div>Loading...</div>
      }

      return (
        <div>
          <p data-testid="auth-status">{isAuthenticated ? 'Authenticated' : 'Not authenticated'}</p>
          <p data-testid="email-display">{session?.email ?? 'No email'}</p>
          <button onClick={() => void login()}>Login</button>
        </div>
      )
    }

    it('updates UI when login() resolves the session', async () => {
      mockAuthStatus({ status: 401 })
      const user = userEvent.setup()

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      )

      await waitFor(() => {
        expect(screen.queryByText('Loading...')).not.toBeInTheDocument()
      })

      expect(screen.getByTestId('auth-status')).toHaveTextContent('Not authenticated')

      mockAuthStatus({ body: { isAuthorized: true, email: 'user@example.com' } })
      await user.click(screen.getByRole('button', { name: /login/i }))

      await waitFor(() => {
        expect(screen.getByTestId('auth-status')).toHaveTextContent('Authenticated')
      })
      expect(screen.getByTestId('email-display')).toHaveTextContent('user@example.com')
    })
  })
})
