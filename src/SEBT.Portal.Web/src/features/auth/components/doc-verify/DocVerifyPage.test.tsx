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
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace
  })
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

function setChallengeContext(challengeId: string, allowIdRetry = false, subState?: string) {
  sessionStorage.setItem('docVerify_challengeId', challengeId)
  sessionStorage.setItem('docVerify_allowIdRetry', String(allowIdRetry))
  if (subState) {
    sessionStorage.setItem('docVerify_subState', subState)
  }
}

describe('DocVerifyPage', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockReplace.mockClear()
    sessionStorage.clear()
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
      setChallengeContext('challenge-abc', true)

      renderWithProviders(
        <DocVerifyPage
          contactLink={TEST_CONTACT_LINK}
          sdkKey={TEST_SDK_KEY}
        />
      )

      expect(await screen.findByRole('button', { name: /enter an id number/i })).toBeInTheDocument()
    })

    it('hides "Enter an ID number" button when allowIdRetry is false', async () => {
      setChallengeContext('challenge-abc', false)

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
      setChallengeContext('challenge-abc', true)
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
  })

  describe('Continue → capture → pending flow', () => {
    it('"Continue" triggers challenge start and persists capture sub-state', async () => {
      setChallengeContext('mock-challenge-123')
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
      setChallengeContext('challenge-abc', false, 'capture')

      // Override verification status to return verified immediately
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
      setChallengeContext('challenge-abc', false, 'pending')

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
      setChallengeContext('challenge-abc', false, 'pending')

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
