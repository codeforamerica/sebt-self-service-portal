import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { SignOutLink } from './SignOutLink'

const mockPush = vi.fn()
const mockLogout = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush
  })
}))

vi.mock('../../context', () => ({
  useAuth: () => ({
    logout: mockLogout
  })
}))

describe('SignOutLink', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockLogout.mockClear()
  })

  it('renders a sign-out button', () => {
    render(<SignOutLink />)

    expect(screen.getByRole('button', { name: /logout|sign out/i })).toBeInTheDocument()
  })

  it('calls logout and redirects to /login when clicked', async () => {
    const user = userEvent.setup()
    render(<SignOutLink />)

    const button = screen.getByRole('button', { name: /logout|sign out/i })
    await user.click(button)

    expect(mockLogout).toHaveBeenCalledTimes(1)
    expect(mockPush).toHaveBeenCalledWith('/login')
  })
})
