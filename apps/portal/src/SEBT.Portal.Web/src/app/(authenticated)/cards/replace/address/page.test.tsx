import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import CardReplaceAddressPage from './page'

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('case=case-1')
}))

vi.mock('@/features/address', () => ({
  AddressFlowProvider: ({ children }: { children: ReactNode }) => <>{children}</>
}))

vi.mock('@/features/address/components/AddressForm', () => ({
  AddressForm: () => <div data-testid="address-form" />
}))

let mockIsLoading = true
vi.mock('@/features/household', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/household')>()
  return {
    ...actual,
    useHouseholdData: () => ({ data: undefined, isLoading: mockIsLoading, isError: false })
  }
})

describe('CardReplaceAddressPage', () => {
  beforeEach(() => {
    mockIsLoading = true
  })

  it('renders the loading copy, not a raw translation key, while household data loads', () => {
    render(<CardReplaceAddressPage />)

    expect(screen.getByText('Loading...')).toBeInTheDocument()
    expect(screen.queryByText('loading')).not.toBeInTheDocument()
  })
})
