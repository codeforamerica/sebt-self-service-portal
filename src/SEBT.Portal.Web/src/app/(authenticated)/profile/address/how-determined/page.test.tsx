import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import HowAddressDeterminedPage from './page'

const mockReplace = vi.fn()
const mockBack = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: mockReplace,
    back: mockBack
  })
}))

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => mockState
  }
})

describe('HowAddressDeterminedPage', () => {
  beforeEach(() => {
    mockReplace.mockClear()
    mockBack.mockClear()
    mockState = 'dc'
  })

  it('renders the informational content for DC', () => {
    render(<HowAddressDeterminedPage />)

    expect(
      screen.getByRole('heading', { name: /Mailing address for SNAP or TANF EBT/i })
    ).toBeInTheDocument()
    expect(
      screen.getByText(/mailing address we have is the one listed on your household/i)
    ).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('links to the replacement card info page', () => {
    render(<HowAddressDeterminedPage />)
    const link = screen.getByRole('link', {
      name: 'Tap here to learn how to get a replacement SNAP or TANF EBT card'
    })
    expect(link).toHaveAttribute('href', '/cards/info')
  })

  it('links to the contact preferences page', () => {
    render(<HowAddressDeterminedPage />)
    const link = screen.getByRole('link', {
      name: 'Tap here to update your contact preferences'
    })
    expect(link).toHaveAttribute('href', '/contact')
  })

  it('calls router.back when the Back button is clicked', () => {
    render(<HowAddressDeterminedPage />)
    screen.getByRole('button', { name: 'Back' }).click()
    expect(mockBack).toHaveBeenCalled()
  })

  it('redirects non-DC users to the dashboard', async () => {
    mockState = 'co'
    render(<HowAddressDeterminedPage />)

    // router.replace is dispatched from useEffect; wait for it to settle.
    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/dashboard')
    })
    expect(
      screen.queryByRole('heading', { name: /Mailing address for SNAP or TANF EBT/i })
    ).not.toBeInTheDocument()
  })
})
