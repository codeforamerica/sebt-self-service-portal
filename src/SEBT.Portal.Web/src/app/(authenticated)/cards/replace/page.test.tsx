import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { HouseholdData } from '@/features/household'

import CardReplacePage from './page'

let mockSearchParams = new URLSearchParams()
vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams
}))

vi.mock('@/hooks/useFlowStartAnalytics', () => ({
  useFlowStartAnalytics: vi.fn()
}))

let mockHouseholdData: HouseholdData | null = null
let mockIsLoading = false
let mockIsError = false
vi.mock('@/features/household', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/household')>()
  return {
    ...actual,
    useHouseholdData: () => ({
      data: mockHouseholdData,
      isLoading: mockIsLoading,
      isError: mockIsError
    })
  }
})

// Keep the test focused on this page's own branches; ConfirmAddress has its
// own suite.
vi.mock('@/features/cards/components/ConfirmAddress', () => ({
  ConfirmAddress: () => <div data-testid="confirm-address" />
}))

// Mock react-i18next with namespace-aware resolution. The `dev` namespace owns
// the "loading" key; `common` does not. A second-arg English fallback would
// mask a wrong-namespace lookup, so this mock has NO fallback support — the
// page must request the key from the namespace that actually defines it.
const NAMESPACE_KEYS: Record<string, Record<string, string>> = {
  dev: { loading: 'Loading...' },
  common: {},
  validation: { globalInternalError: 'An error occurred on our end. Please try again.' }
}
vi.mock('react-i18next', () => ({
  useTranslation: (ns: string) => ({
    t: (key: string) => NAMESPACE_KEYS[ns]?.[key] ?? key,
    i18n: { language: 'en' }
  })
}))

describe('CardReplacePage', () => {
  beforeEach(() => {
    mockSearchParams = new URLSearchParams()
    mockHouseholdData = null
    mockIsLoading = false
    mockIsError = false
  })

  it('shows real loading copy while household data is fetching', () => {
    mockIsLoading = true
    render(<CardReplacePage />)

    expect(screen.getByText('Loading...')).toBeInTheDocument()
  })

  it('renders the generic error copy from the validation namespace when the fetch fails', () => {
    mockIsError = true
    render(<CardReplacePage />)

    expect(screen.getByText('An error occurred on our end. Please try again.')).toBeInTheDocument()
  })
})
