import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import { clearCachedOutageFlag, writeCachedOutageFlag } from '@/features/outage/outageFlagCache'
import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import { OutageGuard } from './OutageGuard'

const mockReplace = vi.fn()
const mockPathname = vi.fn(() => '/dashboard')

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace
  }),
  usePathname: () => mockPathname()
}))

function renderWithFlags(
  overrides: Partial<Record<keyof typeof TEST_FEATURE_FLAGS, boolean>> = {},
  options: { isLoading?: boolean } = {}
) {
  const flags: FeatureFlagsContextValue = {
    flags: { ...TEST_FEATURE_FLAGS, ...overrides },
    isLoading: options.isLoading ?? false,
    isError: false
  }

  return render(
    <FeatureFlagsContext.Provider value={flags}>
      <OutageGuard>
        <div>Portal Content</div>
      </OutageGuard>
    </FeatureFlagsContext.Provider>
  )
}

describe('OutageGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    clearCachedOutageFlag()
    mockPathname.mockReturnValue('/dashboard')
  })

  it('renders children when outage_page_enabled is false', () => {
    renderWithFlags({ outage_page_enabled: false })

    expect(screen.getByText('Portal Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to /outage when outage_page_enabled is true', () => {
    renderWithFlags({ outage_page_enabled: true })

    expect(screen.queryByText('Portal Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('renders children on /outage when outage_page_enabled is true', () => {
    mockPathname.mockReturnValue('/outage')
    renderWithFlags({ outage_page_enabled: true })

    expect(screen.getByText('Portal Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects away from /outage when outage_page_enabled is false', () => {
    mockPathname.mockReturnValue('/outage')
    renderWithFlags({ outage_page_enabled: false })

    expect(screen.queryByText('Portal Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/login')
  })

  it('renders children while feature flags load when outage is not cached as enabled', () => {
    renderWithFlags({ outage_page_enabled: true }, { isLoading: true })

    expect(screen.getByText('Portal Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('blocks and redirects while feature flags load when outage is cached as enabled', async () => {
    writeCachedOutageFlag(true)
    renderWithFlags({ outage_page_enabled: false }, { isLoading: true })

    await waitFor(() => {
      expect(screen.queryByText('Portal Content')).not.toBeInTheDocument()
    })
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('persists the live flag to sessionStorage after features load', () => {
    renderWithFlags({ outage_page_enabled: true })

    expect(window.sessionStorage.getItem('sebt_outage_page_enabled')).toBe('true')
  })
})
