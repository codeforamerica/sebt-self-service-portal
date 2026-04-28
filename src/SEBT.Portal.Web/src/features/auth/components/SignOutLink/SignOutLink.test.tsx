import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { SignOutLink } from './SignOutLink'

describe('SignOutLink', () => {
  it('renders a sign-out link', () => {
    render(<SignOutLink />)

    expect(screen.getByRole('link', { name: /logout|sign out/i })).toBeInTheDocument()
  })

  it('links to /api/auth/logout', () => {
    render(<SignOutLink />)

    const link = screen.getByRole('link', { name: /logout|sign out/i })
    expect(link).toHaveAttribute('href', '/api/auth/logout')
  })
})
