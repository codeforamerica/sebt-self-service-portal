import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { clearCachedOutageFlag, writeCachedOutageFlag } from '@/features/outage/outageFlagCache'
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

describe('OutageGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    clearCachedOutageFlag()
    mockPathname.mockReturnValue('/check')
  })

  it('renders children when no outage is active', () => {
    renderGuard({ outageActive: false })

    expect(screen.getByText('Checker Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to /outage when an outage is active', () => {
    renderGuard({ outageActive: true })

    expect(screen.queryByText('Checker Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('renders children on /outage while the outage is active', () => {
    mockPathname.mockReturnValue('/outage')
    renderGuard({ outageActive: true })

    expect(screen.getByText('Checker Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects away from /outage when the outage has ended', () => {
    mockPathname.mockReturnValue('/outage')
    renderGuard({ outageActive: false })

    expect(screen.queryByText('Checker Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/')
  })

  it('renders children while features load when no outage is cached', () => {
    renderGuard({ outageActive: false, isPending: true })

    expect(screen.getByText('Checker Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('blocks and redirects while features load when an outage is cached as active', async () => {
    writeCachedOutageFlag(true)
    renderGuard({ outageActive: false, isPending: true })

    await waitFor(() => {
      expect(screen.queryByText('Checker Content')).not.toBeInTheDocument()
    })
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('persists the live outage state to sessionStorage after features load', () => {
    renderGuard({ outageActive: true })

    expect(window.sessionStorage.getItem('sebt_checker_outage_page_enabled')).toBe('true')
  })
})
