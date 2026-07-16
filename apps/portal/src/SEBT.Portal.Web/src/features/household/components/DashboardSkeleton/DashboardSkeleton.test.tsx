import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { DashboardSkeleton } from './DashboardSkeleton'

describe('DashboardSkeleton', () => {
  it('renders with loading status role', () => {
    render(<DashboardSkeleton />)

    const skeleton = screen.getByRole('status')
    expect(skeleton).toBeInTheDocument()
    expect(skeleton).toHaveAttribute('aria-label', 'Loading dashboard')
  })

  it('renders screen reader text', () => {
    render(<DashboardSkeleton />)

    expect(screen.getByText('Loading dashboard')).toHaveClass('usa-sr-only')
  })

  it('renders skeleton sections with aria-hidden', () => {
    const { container } = render(<DashboardSkeleton />)

    // Skeleton boxes should be hidden from assistive technology
    const skeletonBoxes = container.querySelectorAll('[aria-hidden="true"]')
    expect(skeletonBoxes.length).toBeGreaterThan(0)
  })

  it('renders pulse animation styles', () => {
    const { container } = render(<DashboardSkeleton />)

    const style = container.querySelector('style')
    expect(style).toBeInTheDocument()
    expect(style?.textContent).toContain('@keyframes pulse')
  })
})
