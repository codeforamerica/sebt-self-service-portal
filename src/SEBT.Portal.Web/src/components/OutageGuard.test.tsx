import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
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

// The redirect choreography itself is covered in @sebt/design-system. These tests pin the wiring
// this app owns: which flag supplies outage state, where it sends people once the outage ends, and
// which sessionStorage key it caches under.
describe('OutageGuard (portal wiring)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.sessionStorage.clear()
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

  it('treats a loading feature-flags context as still resolving', () => {
    renderWithFlags({ outage_page_enabled: true }, { isLoading: true })

    expect(screen.getByText('Portal Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('returns to login, not the checker landing route, when the outage ends', () => {
    mockPathname.mockReturnValue('/outage')
    renderWithFlags({ outage_page_enabled: false })

    expect(mockReplace).toHaveBeenCalledWith('/login')
  })

  it('caches under the portal key so a checker outage does not gate the portal', () => {
    renderWithFlags({ outage_page_enabled: true })

    expect(window.sessionStorage.getItem('sebt_outage_page_enabled')).toBe('true')
    expect(window.sessionStorage.getItem('sebt_checker_outage_page_enabled')).toBeNull()
  })
})
