import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { useOutageState } from '@/features/outage/useOutageState'

import { OutageGuard } from './OutageGuard'

const mockReplace = vi.fn()
const mockPathname = vi.fn(() => '/check')

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace
  }),
  usePathname: () => mockPathname()
}))

vi.mock('@/features/outage/useOutageState', () => ({
  useOutageState: vi.fn()
}))

// stateConfig evaluates the t3-env schema at module scope, which requires real env vars.
vi.mock('@/lib/stateConfig', () => ({
  getEnrollmentConfig: () => ({ apiBaseUrl: '' })
}))

const mockUseOutageState = vi.mocked(useOutageState)

function renderGuard(state: { outageActive: boolean; isPending?: boolean }) {
  mockUseOutageState.mockReturnValue({
    outageActive: state.outageActive,
    isPending: state.isPending ?? false
  })

  return render(
    <OutageGuard>
      <div>Checker Content</div>
    </OutageGuard>
  )
}

// The redirect choreography itself is covered in @sebt/design-system. These tests pin the wiring
// this app owns: which hook supplies outage state, where it sends people once the outage ends, and
// which sessionStorage key it caches under.
describe('OutageGuard (checker wiring)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.sessionStorage.clear()
    mockPathname.mockReturnValue('/check')
  })

  it('renders children when useOutageState reports no outage', () => {
    renderGuard({ outageActive: false })

    expect(screen.getByText('Checker Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to /outage when useOutageState reports an outage', () => {
    renderGuard({ outageActive: true })

    expect(screen.queryByText('Checker Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('returns to the landing route, not the portal login, when the outage ends', () => {
    mockPathname.mockReturnValue('/outage')
    renderGuard({ outageActive: false })

    expect(mockReplace).toHaveBeenCalledWith('/')
  })

  it('caches under the checker key so a portal outage does not gate the checker', () => {
    renderGuard({ outageActive: true })

    expect(window.sessionStorage.getItem('sebt_checker_outage_page_enabled')).toBe('true')
    expect(window.sessionStorage.getItem('sebt_outage_page_enabled')).toBeNull()
  })
})
