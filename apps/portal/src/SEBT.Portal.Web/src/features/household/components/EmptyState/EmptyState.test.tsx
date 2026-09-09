import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext } from '@/features/feature-flags'

import { EmptyState } from './EmptyState'

vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: vi.fn().mockReturnValue('dc')
  }
})

const { getState } = await import('@sebt/design-system')
const mockGetState = vi.mocked(getState)

function withApplyFlag(children: ReactNode, enableApply: boolean) {
  return (
    <FeatureFlagsContext.Provider
      value={{ flags: { enable_apply: enableApply }, isLoading: false, isError: false }}
    >
      {children}
    </FeatureFlagsContext.Provider>
  )
}

describe('EmptyState', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
  })

  it('renders a warning alert', () => {
    render(<EmptyState />)

    const alert = screen.getByRole('alert')
    expect(alert).toBeInTheDocument()
  })

  it('does not render an apply link for DC even when applications are open', () => {
    render(withApplyFlag(<EmptyState />, true))

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('renders the CO apply link pointing at the PEAK form when applications are open', () => {
    mockGetState.mockReturnValue('co')
    render(withApplyFlag(<EmptyState />, true))

    const link = screen.getByRole('link')
    expect(link.getAttribute('href')).toContain(
      'peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'
    )
  })

  it('does not render the CO apply link when the enable_apply flag is off', () => {
    mockGetState.mockReturnValue('co')
    render(withApplyFlag(<EmptyState />, false))

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('does not render the CO apply link outside the feature-flags provider (fail closed)', () => {
    mockGetState.mockReturnValue('co')
    render(<EmptyState />)

    expect(screen.queryByRole('link')).toBeNull()
  })
})
