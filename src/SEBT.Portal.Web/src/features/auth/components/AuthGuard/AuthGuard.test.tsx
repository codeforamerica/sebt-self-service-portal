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

describe('AuthGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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

  it('renders nothing while loading', () => {
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
})
