import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { server } from '@/mocks/server'

import { AuthProvider } from '../../context'
import { DocVerifyPage } from './DocVerifyPage'

const TEST_CONTACT_LINK = 'https://example.com/contact'
const TEST_SDK_KEY = 'test-sdk-key'

// Mock next/navigation
const mockPush = vi.fn()
const mockReplace = vi.fn()
const mockSearchParams = new URLSearchParams()
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace
  }),
  useSearchParams: () => mockSearchParams
}))

// Use the mock adapter in tests
vi.stubEnv('NEXT_PUBLIC_MOCK_SOCURE', 'true')

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

// Set challenge context in sessionStorage (fallback path for mobile recovery).
// allowIdRetry is no longer stored here — it comes from the status API (D9).
function setChallengeContext(challengeId: string, subState?: string) {
  sessionStorage.setItem('docVerify_challengeId', challengeId)
  if (subState) {
    sessionStorage.setItem('docVerify_subState', subState)
  }
}

describe('DocVerifyPage', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockReplace.mockClear()
    sessionStorage.clear()
    // Reset URL search params between tests
    mockSearchParams.delete('challengeId')
  })

  describe('Route guard', () => {
    it('redirects to id-proofing when no challenge context is present', async () => {
      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/login/id-proofing')
      })
    })
  })

  describe('Interstitial sub-state', () => {
    it('renders interstitial when challenge context is present', async () => {
      setChallengeContext('challenge-abc')

      // Status API returns pending with no allowIdRetry
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      expect(
        await screen.findByRole('heading', { name: /we want to keep your account safe/i })
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
    })

    it('shows "Enter an ID number" button when allowIdRetry is true', async () => {
      setChallengeContext('challenge-abc')

      // allowIdRetry comes from the status API response (D9)
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: true })
        })
      )

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      expect(await screen.findByRole('button', { name: /enter an id number/i })).toBeInTheDocument()
    })

    it('hides "Enter an ID number" button when allowIdRetry is false', async () => {
      setChallengeContext('challenge-abc')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending', allowIdRetry: false })
        })
      )

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

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

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await user.click(await screen.findByRole('button', { name: /enter an id number/i }))

      expect(mockPush).toHaveBeenCalledWith('/login/id-proofing')
      expect(sessionStorage.getItem('docVerify_challengeId')).toBeNull()
    })

    it('reads challengeId from URL query param as primary source', async () => {
      // Set challengeId in URL (primary source)
      mockSearchParams.set('challengeId', 'url-challenge-id')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      // Should render interstitial (not redirect), proving the URL challengeId was used
      expect(
        await screen.findByRole('heading', { name: /we want to keep your account safe/i })
      ).toBeInTheDocument()

      // Should persist to sessionStorage for mobile recovery
      expect(sessionStorage.getItem('docVerify_challengeId')).toBe('url-challenge-id')
    })
  })

  describe('Continue → capture → pending flow', () => {
    it('"Continue" triggers challenge start and persists capture sub-state', async () => {
      setChallengeContext('mock-challenge-123')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      const user = userEvent.setup()

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      // After click → JIT token fetch → capture sub-state, the interstitial disappears
      await waitFor(() => {
        expect(
          screen.queryByRole('heading', { name: /we want to keep your account safe/i })
        ).not.toBeInTheDocument()
      })

      // Sub-state should be persisted for mobile tab recovery
      expect(sessionStorage.getItem('docVerify_subState')).not.toBeNull()
    })

    it('full flow: Continue → capture → pending (mock adapter onSuccess)', async () => {
      setChallengeContext('mock-challenge-123')

      // Keep status as pending so the pending UI stays visible
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      const user = userEvent.setup()

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      // Mock adapter fires onSuccess after ~1500ms → transitions to pending
      await waitFor(
        () => {
          expect(screen.getByText(/verifying your document/i)).toBeInTheDocument()
        },
        { timeout: 3000 }
      )
    })
  })

  describe('SessionStorage recovery (D6)', () => {
    it('resumes at pending when persisted sub-state was capture', async () => {
      setChallengeContext('challenge-abc', 'capture')

      // Override verification status to return pending
      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        })
      )

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

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

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/dashboard')
      })

      // Challenge context should be cleared
      expect(sessionStorage.getItem('docVerify_challengeId')).toBeNull()
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

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/login/id-proofing/off-boarding')
      })

      expect(sessionStorage.getItem('offboarding_reason')).toBe('docVerificationFailed')
    })
  })

  describe('Error handling', () => {
    it('shows error alert when challenge start fails', async () => {
      setChallengeContext('challenge-abc')

      server.use(
        http.get('/api/id-proofing/status', () => {
          return HttpResponse.json({ status: 'pending' })
        }),
        http.get('/api/challenges/:id/start', () => {
          return HttpResponse.json({ error: 'Challenge expired' }, { status: 400 })
        })
      )

      const user = userEvent.setup()

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      await user.click(await screen.findByRole('button', { name: /continue/i }))

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/something went wrong/i)
      })
    })
  })
})
