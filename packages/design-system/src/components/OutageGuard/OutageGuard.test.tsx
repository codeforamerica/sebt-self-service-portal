import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { OutageGuard } from './OutageGuard'

const mockReplace = vi.fn()
const mockPathname = vi.fn(() => '/check')

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace
  }),
  usePathname: () => mockPathname()
}))

const STORAGE_KEY = 'test_outage_page_enabled'

function renderGuard(props: { outageActive: boolean; isResolving?: boolean; offPath?: string }) {
  return render(
    <OutageGuard
      outageActive={props.outageActive}
      isResolving={props.isResolving ?? false}
      offPath={props.offPath ?? '/'}
      storageKey={STORAGE_KEY}
    >
      <div>Guarded Content</div>
    </OutageGuard>
  )
}

describe('OutageGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.sessionStorage.clear()
    mockPathname.mockReturnValue('/check')
  })

  it('renders children when no outage is active', () => {
    renderGuard({ outageActive: false })

    expect(screen.getByText('Guarded Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects to /outage when an outage is active', () => {
    renderGuard({ outageActive: true })

    expect(screen.queryByText('Guarded Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('renders children on /outage while the outage is active', () => {
    mockPathname.mockReturnValue('/outage')
    renderGuard({ outageActive: true })

    expect(screen.getByText('Guarded Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  // Each app names its own landing route: the portal sends people to /login, the checker to /.
  it('redirects away from /outage to offPath when the outage has ended', () => {
    mockPathname.mockReturnValue('/outage')
    renderGuard({ outageActive: false, offPath: '/login' })

    expect(screen.queryByText('Guarded Content')).not.toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/login')
  })

  it('renders children while resolving when no outage is cached', () => {
    renderGuard({ outageActive: false, isResolving: true })

    expect(screen.getByText('Guarded Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  // Without the cache, a mid-outage navigation flashes the page it is about to redirect away from.
  it('blocks and redirects while resolving when an outage is cached as active', async () => {
    window.sessionStorage.setItem(STORAGE_KEY, 'true')
    renderGuard({ outageActive: false, isResolving: true })

    await waitFor(() => {
      expect(screen.queryByText('Guarded Content')).not.toBeInTheDocument()
    })
    expect(mockReplace).toHaveBeenCalledWith('/outage')
  })

  it('does not block while resolving when the cache says the outage ended', () => {
    window.sessionStorage.setItem(STORAGE_KEY, 'false')
    renderGuard({ outageActive: false, isResolving: true })

    expect(screen.getByText('Guarded Content')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('persists the resolved outage state to sessionStorage', () => {
    renderGuard({ outageActive: true })

    expect(window.sessionStorage.getItem(STORAGE_KEY)).toBe('true')
  })

  it('does not persist while the outage state is still resolving', () => {
    renderGuard({ outageActive: true, isResolving: true })

    expect(window.sessionStorage.getItem(STORAGE_KEY)).toBeNull()
  })

  it('reads and writes only the key it was given', () => {
    renderGuard({ outageActive: true })

    expect(window.sessionStorage.getItem(STORAGE_KEY)).toBe('true')
    expect(window.sessionStorage.getItem('sebt_outage_page_enabled')).toBeNull()
    expect(window.sessionStorage.getItem('sebt_checker_outage_page_enabled')).toBeNull()
  })
})
