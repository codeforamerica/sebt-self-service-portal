import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { EmptyState } from './EmptyState'

describe('EmptyState', () => {
  it('renders a warning alert', () => {
    render(<EmptyState />)

    const alert = screen.getByRole('alert')
    expect(alert).toBeInTheDocument()
  })

  it('renders the apply link', () => {
    render(<EmptyState />)

    const link = screen.getByRole('link')
    expect(link).toHaveAttribute('href', '/apply')
  })
})
