import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import CoLoadedInfoPage from './page'

const mockReplace = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace
  })
}))

let mockState = 'dc'
vi.mock('@/lib/state', () => ({
  getState: () => mockState
}))

vi.mock('@/features/address/components/CoLoadedInfo', () => ({
  CoLoadedInfo: () => <div data-testid="co-loaded-info">CoLoadedInfo content</div>
}))

describe('CoLoadedInfoPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockState = 'dc'
  })

  it('renders CoLoadedInfo content for DC', () => {
    mockState = 'dc'
    render(<CoLoadedInfoPage />)

    expect(screen.getByTestId('co-loaded-info')).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('redirects non-DC users to address form', () => {
    mockState = 'co'
    render(<CoLoadedInfoPage />)

    expect(mockReplace).toHaveBeenCalledWith('/profile/address')
    expect(screen.queryByTestId('co-loaded-info')).not.toBeInTheDocument()
  })
})
