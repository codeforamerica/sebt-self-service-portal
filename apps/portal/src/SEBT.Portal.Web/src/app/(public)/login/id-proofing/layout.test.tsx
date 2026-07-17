import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mockUsePathname = vi.fn<() => string>()
vi.mock('next/navigation', () => ({
  usePathname: () => mockUsePathname()
}))

vi.mock('@/features/auth', () => ({
  AuthGuard: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="auth-guard">{children}</div>
  )
}))

import IdProofingLayout from './layout'

function renderAt(pathname: string) {
  mockUsePathname.mockReturnValue(pathname)
  return render(
    <IdProofingLayout>
      <span data-testid="child">child</span>
    </IdProofingLayout>
  )
}

describe('IdProofingLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('wraps id-proofing pages in AuthGuard', () => {
    renderAt('/login/id-proofing')
    expect(screen.getByTestId('auth-guard')).toBeInTheDocument()
    expect(screen.getByTestId('child')).toBeInTheDocument()
  })

  it('renders off-boarding without AuthGuard so users who lost their session during OIDC step-up still see the failure screen', () => {
    renderAt('/login/id-proofing/off-boarding')
    expect(screen.queryByTestId('auth-guard')).not.toBeInTheDocument()
    expect(screen.getByTestId('child')).toBeInTheDocument()
  })
})
