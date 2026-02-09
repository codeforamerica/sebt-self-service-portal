/**
 * AuthContext Unit Tests
 *
 * Tests the authentication context including:
 * - Token storage and retrieval
 * - Login and logout functionality
 * - Session storage persistence
 * - Loading state management
 */
import { act, render, renderHook, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider, getAuthToken, useAuth } from './AuthContext'

const TEST_TOKEN = 'test-jwt-token-12345'

describe('AuthContext', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  afterEach(() => {
    sessionStorage.clear()
  })

  describe('AuthProvider', () => {
    it('should provide initial unauthenticated state', async () => {
      const { result } = renderHook(() => useAuth(), {
        wrapper: AuthProvider
      })

      // Wait for loading to complete
      await waitFor(() => {
        expect(result.current.isLoading).toBe(false)
      })

      expect(result.current.token).toBeNull()
      expect(result.current.isAuthenticated).toBe(false)
    })

    it('should load token from sessionStorage on mount', async () => {
      sessionStorage.setItem('auth_token', TEST_TOKEN)

      const { result } = renderHook(() => useAuth(), {
        wrapper: AuthProvider
      })

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false)
      })

      expect(result.current.token).toBe(TEST_TOKEN)
      expect(result.current.isAuthenticated).toBe(true)
    })

    it('should store token in sessionStorage on login', async () => {
      const { result } = renderHook(() => useAuth(), {
        wrapper: AuthProvider
      })

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false)
      })

      act(() => {
        result.current.login(TEST_TOKEN)
      })

      expect(result.current.token).toBe(TEST_TOKEN)
      expect(result.current.isAuthenticated).toBe(true)
      expect(sessionStorage.getItem('auth_token')).toBe(TEST_TOKEN)
    })

    it('should clear token from sessionStorage on logout', async () => {
      sessionStorage.setItem('auth_token', TEST_TOKEN)

      const { result } = renderHook(() => useAuth(), {
        wrapper: AuthProvider
      })

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false)
      })

      expect(result.current.isAuthenticated).toBe(true)

      act(() => {
        result.current.logout()
      })

      expect(result.current.token).toBeNull()
      expect(result.current.isAuthenticated).toBe(false)
      expect(sessionStorage.getItem('auth_token')).toBeNull()
    })
  })

  describe('useAuth', () => {
    it('should throw error when used outside AuthProvider', () => {
      // Suppress console.error for this test
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      expect(() => {
        renderHook(() => useAuth())
      }).toThrow('useAuth must be used within an AuthProvider')

      consoleSpy.mockRestore()
    })
  })

  describe('getAuthToken', () => {
    it('should return null when no token is stored', () => {
      expect(getAuthToken()).toBeNull()
    })

    it('should return token when stored in sessionStorage', () => {
      sessionStorage.setItem('auth_token', TEST_TOKEN)
      expect(getAuthToken()).toBe(TEST_TOKEN)
    })
  })

  describe('Integration with components', () => {
    function TestComponent() {
      const { token, isAuthenticated, isLoading, login, logout } = useAuth()

      if (isLoading) {
        return <div>Loading...</div>
      }

      return (
        <div>
          <p data-testid="auth-status">{isAuthenticated ? 'Authenticated' : 'Not authenticated'}</p>
          <p data-testid="token-display">{token ?? 'No token'}</p>
          <button onClick={() => login(TEST_TOKEN)}>Login</button>
          <button onClick={() => logout()}>Logout</button>
        </div>
      )
    }

    it('should update UI when login is called', async () => {
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
      expect(screen.getByTestId('token-display')).toHaveTextContent('No token')

      await user.click(screen.getByRole('button', { name: /login/i }))

      expect(screen.getByTestId('auth-status')).toHaveTextContent('Authenticated')
      expect(screen.getByTestId('token-display')).toHaveTextContent(TEST_TOKEN)
    })

    it('should update UI when logout is called', async () => {
      const user = userEvent.setup()
      sessionStorage.setItem('auth_token', TEST_TOKEN)

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      )

      await waitFor(() => {
        expect(screen.queryByText('Loading...')).not.toBeInTheDocument()
      })

      expect(screen.getByTestId('auth-status')).toHaveTextContent('Authenticated')

      await user.click(screen.getByRole('button', { name: /logout/i }))

      expect(screen.getByTestId('auth-status')).toHaveTextContent('Not authenticated')
      expect(screen.getByTestId('token-display')).toHaveTextContent('No token')
    })
  })
})
