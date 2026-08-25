import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import RequestReplacementCardsPage from './page'

const mockReplace = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace, push: vi.fn() })
}))

vi.mock('@/hooks/useFlowStartAnalytics', () => ({
  useFlowStartAnalytics: vi.fn()
}))

vi.mock('@/features/address/components/CardSelection', () => ({
  CardSelection: () => <div data-testid="card-selection" />
}))

let mockIsLoading = true
vi.mock('@/features/household', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/household')>()
  return {
    ...actual,
    useHouseholdData: () => ({ data: undefined, isLoading: mockIsLoading, isError: false })
  }
})

describe('RequestReplacementCardsPage', () => {
  beforeEach(() => {
    mockIsLoading = true
  })

  it('announces the loading state with authored copy, not a raw translation key', () => {
    render(<RequestReplacementCardsPage />)

    const status = screen.getByRole('status')
    expect(status).toHaveTextContent('Loading...')
    expect(status).not.toHaveTextContent(/^loading$/)
  })
})
