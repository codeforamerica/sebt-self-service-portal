import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import { AuthProvider } from '../../context'
import { AuthGuard } from '../AuthGuard/AuthGuard'
import { DocVerifyPage } from './DocVerifyPage'

const TEST_CONTACT_LINK = 'https://example.com/contact'

// Mock next/navigation. The router object is memoized so useCallback deps that
// include `router` stay stable across renders, matching the real Next.js hook.
const mockPush = vi.fn()
const mockReplace = vi.fn()
const mockRouter = { push: mockPush, replace: mockReplace }
let mockSearchParams = new URLSearchParams()
vi.mock('next/navigation', () => ({
  useRouter: () => mockRouter,
  useSearchParams: () => mockSearchParams
}))

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  })
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createTestQueryClient()
  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{ui}</AuthProvider>
      </QueryClientProvider>
    ),
    queryClient
  }
}

function setChallengeContext(challengeId: string, subState?: string) {
  // Primary source is URL search params; sessionStorage is fallback for tab recovery
  mockSearchParams = new URLSearchParams({ challengeId })
  sessionStorage.setItem('docVerify_challengeId', challengeId)
  if (subState) {
    sessionStorage.setItem('docVerify_subState', subState)
  }
}

describe('DocVerifyPage', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockReplace.mockClear()
    mockSearchParams = new URLSearchParams()
    sessionStorage.clear()
  })

  describe('Route guard', () => {
    it('redirects to /login when user is not authenticated', async () => {
      setChallengeContext('challenge-abc')

      // Override auth status to return unauthenticated
      server.use(
        http.get('/api/auth/status', () => {
          return new HttpResponse(null, { status: 401 })
        })
      )

      const queryClient = createTestQueryClient()
      render(
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <AuthGuard>
              <DocVerifyPage contactLink={TEST_CONTACT_LINK} />
            </AuthGuard>
          </AuthProvider>
        </QueryClientProvider>
      )

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/login')
      })

      // Should NOT render any doc-verify content
      expect(screen.queryByRole('heading')).not.toBeInTheDocument()
    })

    it('redirects to id-proofing when no challenge context is present', async () => {
      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/login/id-proofing')
      })
    })
  })

  describe('Interstitial sub-state', () => {
    it('renders interstitial when challenge context is present', async () => {
      setChallengeContext('challenge-abc')

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      expect(
        await screen.findByRole('heading', { name: /we want to keep your account safe/i })
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
    })

    it('shows "Enter an ID number" button when status API returns allowIdRetry: true', async () => {
      setChallengeContext('challenge-abc')

      // Override status API to return allowIdRetry: true (D9)
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: true })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      expect(await screen.findByRole('button', { name: /enter an id number/i })).toBeInTheDocument()
    })

    it('hides "Enter an ID number" button when status API returns allowIdRetry: false', async () => {
      setChallengeContext('challenge-abc')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: false })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      // Wait for interstitial to render
      await screen.findByRole('heading', { name: /we want to keep your account safe/i })

      expect(screen.queryByRole('button', { name: /enter an id number/i })).not.toBeInTheDocument()
    })

    it('"Enter an ID number" clears challenge context and navigates to id-proofing', async () => {
      setChallengeContext('challenge-abc')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: true })
        })
      )

      const user = userEvent.setup()

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /enter an id number/i }))

      expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      expect(sessionStorage.getItem('docVerify_challengeId')).toBeNull()
    })
  })

  describe('Continue → Socure hand-off', () => {
    // The default status handler uses a closure counter shared across tests;
    // pin a stable 'pending' response so the interstitial-level status query
    // can't consume counts and preempt the click.
    beforeEach(() => {
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: false })
        }),
        http.get('/api/challenges/:id/start', () => {
          return HttpResponse.json({
            docvTransactionToken: 'test-token',
            docvUrl: 'https://verify.socure.com/#/dv/test-token'
          })
        })
      )
    })

    function mockWindowOpen() {
      // Minimal Window stand-in: the page reads `.closed` and assigns
      // `.location.href`, nothing else.
      const popup = {
        closed: false,
        location: { href: '' },
        close: vi.fn()
      }
      const spy = vi.spyOn(window, 'open').mockImplementation(() => popup as unknown as Window)
      return { popup, spy }
    }

    it('opens Socure capture URL in a new tab and transitions to pending', async () => {
      setChallengeContext('mock-challenge-123')
      const { popup, spy } = mockWindowOpen()
      const user = userEvent.setup()

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      // Popup is opened synchronously on click (user-gesture preservation).
      expect(spy).toHaveBeenCalledWith('about:blank', '_blank')

      // Once the token fetch resolves, the popup is redirected to Socure and
      // the page transitions to the pending/polling view.
      await waitFor(() => {
        expect(popup.location.href).toBe('https://verify.socure.com/#/dv/test-token')
      })
      await waitFor(() => {
        expect(screen.getByText(/verifying your document/i)).toBeInTheDocument()
      })

      // Sub-state persisted for mobile tab recovery
      expect(sessionStorage.getItem('docVerify_subState')).toBe('pending')

      spy.mockRestore()
    })

    it('redirects to dashboard when webhook resolves after hand-off', async () => {
      setChallengeContext('mock-challenge-123')
      const { spy } = mockWindowOpen()

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'verified' })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      await waitFor(
        () => {
          expect(mockPush).toHaveBeenCalledWith('/dashboard')
        },
        { timeout: 1500 }
      )

      spy.mockRestore()
    })

    it('redirects to off-boarding when webhook rejects after hand-off', async () => {
      setChallengeContext('mock-challenge-123')
      const { spy } = mockWindowOpen()

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({
            status: 'rejected',
            offboardingReason: 'docVerificationFailed'
          })
        })
      )

      const user = userEvent.setup()
      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      await waitFor(
        () => {
          expect(mockPush).toHaveBeenCalledWith(
            '/login/id-proofing/off-boarding?reason=docVerificationFailed'
          )
        },
        { timeout: 1500 }
      )

      spy.mockRestore()
    })
  })

  describe('Stale sessionStorage detection', () => {
    it('shows interstitial when persisted challengeId does not match URL challengeId', async () => {
      // Simulate stale sessionStorage from a prior DocV attempt
      sessionStorage.setItem('docVerify_challengeId', 'old-challenge-id')
      sessionStorage.setItem('docVerify_subState', 'pending')

      // URL has a different (current) challengeId
      mockSearchParams = new URLSearchParams({ challengeId: 'new-challenge-id' })

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      // Should render the interstitial, NOT the VerificationPending screen
      expect(
        await screen.findByRole('heading', { name: /we want to keep your account safe/i })
      ).toBeInTheDocument()
      expect(screen.queryByText(/verifying your document/i)).not.toBeInTheDocument()
    })

    it('resumes at pending when persisted challengeId matches URL challengeId', async () => {
      // Same challengeId in both sessionStorage and URL
      setChallengeContext('challenge-abc', 'pending')

      // Override verification status to return pending so it stays on the pending screen
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(screen.getByText(/verifying your document/i)).toBeInTheDocument()
      })
    })
  })

  describe('SessionStorage recovery (D6)', () => {
    it('resumes at pending when persisted sub-state was capture', async () => {
      setChallengeContext('challenge-abc', 'capture')

      // Override verification status to return verified immediately
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(screen.getByText(/verifying your document/i)).toBeInTheDocument()
      })
    })
  })

  describe('Pending → result routing', () => {
    it('navigates to dashboard when verification succeeds', async () => {
      setChallengeContext('challenge-abc', 'pending')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'verified' })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/dashboard')
      })

      // Challenge context should be cleared
      expect(sessionStorage.getItem('docVerify_challengeId')).toBeNull()
    })

    it('refreshes the JWT before navigating to dashboard on verified', async () => {
      setChallengeContext('challenge-abc', 'pending')

      // Record the order in which the refresh endpoint resolves and the
      // router navigates. The dashboard carries stale IAL claims if we
      // navigate before the fresh Set-Cookie arrives (DC-296 race).
      const callOrder: string[] = []

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'verified' })
        }),
        http.post('/api/auth/refresh', async () => {
          // Simulate network latency so navigation cannot race ahead.
          await new Promise((resolve) => setTimeout(resolve, 50))
          callOrder.push('refresh')
          return new HttpResponse(null, { status: 204 })
        })
      )

      mockPush.mockImplementation((path: string) => {
        if (path === '/dashboard') {
          callOrder.push('navigate')
        }
      })

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/dashboard')
      })

      // Refresh must be awaited before navigation so the dashboard's first
      // fetches carry the rotated JWT with IAL1+.
      expect(callOrder).toEqual(['refresh', 'navigate'])
    })

    it('still navigates to dashboard when refresh fails', async () => {
      setChallengeContext('challenge-abc', 'pending')

      let refreshCalled = false

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'verified' })
        }),
        http.post('/api/auth/refresh', () => {
          refreshCalled = true
          return HttpResponse.json({ error: 'Server error' }, { status: 500 })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      // A failed refresh must not trap the user on the "verified" screen.
      // Allow extra time because the refresh mutation may retry on 5xx per
      // useRefreshToken's retry policy before the catch fires.
      await waitFor(
        () => {
          expect(mockPush).toHaveBeenCalledWith('/dashboard')
        },
        { timeout: 5000 }
      )

      // And refresh must have been attempted. Otherwise we would have shipped
      // the original bug (navigating with stale JWT) alongside the fallback.
      expect(refreshCalled).toBe(true)
    })

    it('navigates to off-boarding when status endpoint returns 404', async () => {
      setChallengeContext('challenge-abc', 'pending')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ error: 'Challenge not found.' }, { status: 404 })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          '/login/id-proofing/off-boarding?reason=challengeNotFound'
        )
      })
    })

    it('navigates to off-boarding when verification is rejected', async () => {
      setChallengeContext('challenge-abc', 'pending')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({
            status: 'rejected',
            offboardingReason: 'docVerificationFailed'
          })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          '/login/id-proofing/off-boarding?reason=docVerificationFailed'
        )
      })
    })
  })

  describe('Pending → resubmit branch (DC-301)', () => {
    function mockWindowOpen() {
      const popup = {
        closed: false,
        location: { href: '' },
        close: vi.fn()
      }
      const spy = vi.spyOn(window, 'open').mockImplementation(() => popup as unknown as Window)
      return { popup, spy }
    }

    it('renders the retry prompt when status returns resubmit', async () => {
      setChallengeContext('challenge-abc', 'pending')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'resubmit' })
        })
      )

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      expect(
        await screen.findByRole('heading', { name: /let's try that again/i })
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /try again/i })).toBeEnabled()
    })

    it('opens a new tab and swaps to the new challenge ID when Try again is clicked', async () => {
      setChallengeContext('challenge-abc', 'pending')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'resubmit' })
        })
      )

      const { popup, spy } = mockWindowOpen()
      const user = userEvent.setup()

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /try again/i }))

      expect(spy).toHaveBeenCalledWith('about:blank', '_blank')

      await waitFor(() => {
        expect(popup.location.href).toBe('https://verify.socure.com/#/dv/mock-resubmit-token')
      })

      // sessionStorage and URL both swap to the new challenge ID returned by the mutation
      await waitFor(() => {
        expect(sessionStorage.getItem('docVerify_challengeId')).toBe(
          '99999999-9999-4999-8999-999999999999'
        )
      })
      expect(mockReplace).toHaveBeenCalledWith(
        '/login/id-proofing/doc-verify?challengeId=99999999-9999-4999-8999-999999999999'
      )

      spy.mockRestore()
    })
  })

  describe('Error handling', () => {
    it('shows error alert when challenge start fails', async () => {
      setChallengeContext('challenge-abc')

      server.use(
        http.get('/api/challenges/:id/start', () => {
          return HttpResponse.json({ error: 'Challenge expired' }, { status: 400 })
        })
      )

      const user = userEvent.setup()

      renderWithProviders(<DocVerifyPage contactLink={TEST_CONTACT_LINK} />)

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/something went wrong/i)
      })
    })
  })
})
