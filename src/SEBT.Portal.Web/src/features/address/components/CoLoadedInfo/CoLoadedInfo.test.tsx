import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { CoLoadedInfo } from './CoLoadedInfo'

const mockPush = vi.fn()
const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: mockBack
  })
}))

function renderCoLoadedInfo({ showContinue }: { showContinue?: boolean } = {}) {
  const user = userEvent.setup()
  return {
    user,
    ...render(<CoLoadedInfo {...(showContinue != null ? { showContinue } : {})} />)
  }
}

describe('CoLoadedInfo', () => {
  beforeEach(() => {
    mockPush.mockClear()
    mockBack.mockClear()
  })

  // --- Content rendering ---

  it('renders DHS EBT Card Office guidance', () => {
    renderCoLoadedInfo()

    expect(
      screen.getByText(/replacement SNAP or TANF EBT card at a DHS EBT Card Office/i)
    ).toBeInTheDocument()
  })

  it('renders office hours', () => {
    renderCoLoadedInfo()

    expect(screen.getByText(/Monday through Friday.*7:30.*4:45/i)).toBeInTheDocument()
  })

  it('renders office locations', () => {
    renderCoLoadedInfo()

    expect(screen.getByText(/645 H Street NE/)).toBeInTheDocument()
    expect(screen.getByText(/1849 Marion Barry Avenue SE/)).toBeInTheDocument()
  })

  // --- Navigation ---

  it('navigates back when back button is clicked', async () => {
    const { user } = renderCoLoadedInfo()

    await user.click(screen.getByRole('button', { name: /back/i }))
    expect(mockBack).toHaveBeenCalled()
  })

  it('does not render continue button by default', () => {
    renderCoLoadedInfo()

    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /continue/i })).not.toBeInTheDocument()
  })

  it('renders continue button when showContinue is true', async () => {
    const { user } = renderCoLoadedInfo({ showContinue: true })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/dashboard')
  })
})
