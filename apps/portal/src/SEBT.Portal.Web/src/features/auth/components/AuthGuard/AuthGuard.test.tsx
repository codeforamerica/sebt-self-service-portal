import type { StateCode } from '@sebt/design-system'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { AuthGuard } from './AuthGuard'

const mockPush = vi.fn()
const mockReplace = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace
  })
}))

const mockUseAuth = vi.fn()

vi.mock('@/features/auth', () => ({
  useAuth: () => mockUseAuth()
}))

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key })
}))

let mockShowLoadingInterstitial = false
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getStateConfig: (state: StateCode) => ({
      ...actual.getStateConfig(state),
      showLoadingInterstitial: mockShowLoadingInterstitial
    })
  }
})

describe('AuthGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockShowLoadingInterstitial = false
  })

  it('renders children when authenticated', () => {
    mockUseAuth.mockReturnValue({
      isAuthenticated: true,
      isLoading: false
    })

    render(
      <AuthGuard>
        <div>Protected Content</div>
      </AuthGuard>
    )

    expect(screen.getByText('Protected Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to /login when not authenticated', () => {
    mockUseAuth.mockReturnValue({
      isAuthenticated: false,
      isLoading: false
    })

    render(
      <AuthGuard>
        <div>Protected Content</div>
      </AuthGuard>
    )

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/login')
  })

  it('renders nothing while loading when the state keeps the blank loading screen', () => {
    mockUseAuth.mockReturnValue({
      isAuthenticated: false,
      isLoading: true
    })

    const { container } = render(
      <AuthGuard>
        <div>Protected Content</div>
      </AuthGuard>
    )

    expect(container).toBeEmptyDOMElement()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('renders the loading interstitial while loading when the state enables it', () => {
    mockShowLoadingInterstitial = true
    mockUseAuth.mockReturnValue({
      isAuthenticated: false,
      isLoading: true
    })

    render(
      <AuthGuard>
        <div>Protected Content</div>
      </AuthGuard>
    )

    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true')
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })
})
