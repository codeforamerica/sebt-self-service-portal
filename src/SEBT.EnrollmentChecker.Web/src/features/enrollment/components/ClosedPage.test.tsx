import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ClosedPage } from './ClosedPage'

describe('ClosedPage', () => {
  it('renders a heading indicating the checker is unavailable', () => {
    render(<ClosedPage />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })
})
