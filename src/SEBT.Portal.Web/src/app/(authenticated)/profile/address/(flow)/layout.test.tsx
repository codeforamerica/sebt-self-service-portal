import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AddressFlowLayout from './layout'

const mockReplace = vi.fn()
let mockPathname = '/profile/address/replacement-cards'

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({
    replace: mockReplace
  })
}))

// Mock AddressFlowContext with controllable address state
let mockAddress: null | { streetAddress1: string } = null

vi.mock('@/features/address', () => ({
  AddressFlowProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAddressFlow: () => ({
    address: mockAddress,
    setAddress: vi.fn(),
    clearAddress: vi.fn()
  })
}))

describe('AddressFlowLayout / FlowGuard', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockAddress = null
    mockPathname = '/profile/address/replacement-cards'
  })

  it('renders children when address exists in context', () => {
    mockAddress = { streetAddress1: '123 Main St' }
    render(
      <AddressFlowLayout>
        <p>Flow content</p>
      </AddressFlowLayout>
    )

    expect(screen.getByText('Flow content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('renders children on the form page even without address', () => {
    mockPathname = '/profile/address'
    mockAddress = null
    render(
      <AddressFlowLayout>
        <p>Form page</p>
      </AddressFlowLayout>
    )

    expect(screen.getByText('Form page')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to form when address is missing on non-form page', () => {
    mockAddress = null
    render(
      <AddressFlowLayout>
        <p>Should not appear</p>
      </AddressFlowLayout>
    )

    expect(mockReplace).toHaveBeenCalledWith('/profile/address')
    expect(screen.queryByText('Should not appear')).not.toBeInTheDocument()
  })

  it('renders a loading indicator instead of blank content during redirect', () => {
    mockAddress = null
    const { container } = render(
      <AddressFlowLayout>
        <p>Should not appear</p>
      </AddressFlowLayout>
    )

    // Should NOT be an empty container — must have some content for UX
    expect(container.querySelector('[aria-busy="true"]')).toBeInTheDocument()
    // Screen reader users should hear a loading announcement
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })
})
