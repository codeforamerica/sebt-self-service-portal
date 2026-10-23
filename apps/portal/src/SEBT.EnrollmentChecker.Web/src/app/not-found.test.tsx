import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { useApplyHref } from '@/lib/useApplyHref'

import enCo404 from '@/content/locales/en/co/404EnrollmentChecker.json'
import NotFound from './not-found'

// useApplyHref reaches React Query via useCheckerFeatures, so the real hook needs a
// QueryClientProvider this test does not mount. Mocking it also lets each case pick
// the open/closed application state directly.
vi.mock('@/lib/useApplyHref', () => ({
  useApplyHref: vi.fn()
}))

const mockUseApplyHref = vi.mocked(useApplyHref)
const APPLY_HREF = 'https://apply.example.com'

describe('NotFound', () => {
  beforeEach(() => {
    mockUseApplyHref.mockReturnValue(APPLY_HREF)
  })

  it('renders the 404 copy from the generated locale bundle', () => {
    render(<NotFound />)

    const headings = screen.getAllByRole('heading', { level: 1 })
    expect(headings).toHaveLength(1)
    expect(headings[0]).toHaveTextContent(enCo404.title)
    expect(screen.getByText(enCo404.body1)).toBeInTheDocument()
  })

  it('titles the page in the state color and size', () => {
    render(<NotFound />)
    expect(screen.getByRole('heading', { level: 1 })).toHaveClass('font-sans-xl', 'text-primary')
  })

  it('links the action to the configured application destination', () => {
    render(<NotFound />)

    const apply = screen.getByRole('link', { name: enCo404.action })
    expect(apply).toHaveAttribute('href', APPLY_HREF)
    expect(apply).toHaveClass('usa-button')
    expect(apply).toHaveAttribute('data-analytics-cta', 'apply_cta')
  })

  it('drops the action when applications are closed', () => {
    mockUseApplyHref.mockReturnValue(null)
    render(<NotFound />)

    expect(screen.queryByRole('link', { name: enCo404.action })).toBeNull()
    // The apology and explanation must still stand on their own
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(enCo404.title)
  })
})
