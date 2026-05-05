/**
 * Covers the option-set switching logic: co-loaded users see the narrower
 * co-loaded set; everyone else sees the full option list.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import type { SessionInfo } from '../../context'
import type { IdOption } from './IdProofingForm'
import { IdProofingWithDi } from './IdProofingWithDi'

const TEST_CONTACT_LINK = 'https://example.com/contact'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}))

vi.mock('@/features/auth/components/device-intelligence', () => ({
  useDeviceIntelligence: () => ({ getToken: async () => null })
}))

const mockUseAuth = vi.fn()
vi.mock('@/features/auth/context', () => ({
  useAuth: () => mockUseAuth()
}))

const FULL_OPTIONS: IdOption[] = [
  { value: 'ssn', labelKey: 'optionLabelSsn', inputLabelKey: 'labelSsn' },
  { value: 'snapAccountId', labelKey: 'optionAccountId', inputLabelKey: 'labelAccountId' },
  { value: 'snapPersonId', labelKey: 'optionPersonId', inputLabelKey: 'labelPersonId' }
]

const CO_LOADED_OPTIONS: IdOption[] = [
  { value: 'snapAccountId', labelKey: 'optionAccountId', inputLabelKey: 'labelAccountId' },
  { value: 'itin', labelKey: 'optionLabelItin', inputLabelKey: 'labelItin' },
  { value: 'none', labelKey: 'optionLabelNone', dividerBefore: true }
]

const LABEL_SSN = 'Social Security Number (SSN)'
const LABEL_ITIN = 'Individual Taxpayer ID Number (ITIN)'
const LABEL_SNAP_ACCOUNT = 'SNAP or TANF account ID'
const LABEL_SNAP_PERSON = 'SNAP or TANF person ID'

function renderComponent() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <IdProofingWithDi
        idOptions={FULL_OPTIONS}
        coLoadedIdOptions={CO_LOADED_OPTIONS}
        contactLink={TEST_CONTACT_LINK}
      />
    </QueryClientProvider>
  )
}

function session(isCoLoaded: boolean): SessionInfo {
  return {
    email: 'user@example.com',
    ial: '1plus',
    idProofingStatus: 0,
    idProofingCompletedAt: null,
    idProofingExpiresAt: null,
    isCoLoaded,
    expiresAt: null,
    absoluteExpiresAt: null
  }
}

describe('IdProofingWithDi', () => {
  it('renders the co-loaded option set with a divider before "None"', () => {
    mockUseAuth.mockReturnValue({ session: session(true) })

    const { container } = renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SNAP_ACCOUNT })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SSN })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SNAP_PERSON })).not.toBeInTheDocument()
    expect(container.querySelector('hr')).toBeInTheDocument()
  })

  it('does not render a divider for the non-co-loaded option set', () => {
    mockUseAuth.mockReturnValue({ session: session(false) })

    const { container } = renderComponent()

    expect(container.querySelector('hr')).not.toBeInTheDocument()
  })

  it('renders the full option set when session.isCoLoaded is false', () => {
    mockUseAuth.mockReturnValue({ session: session(false) })

    renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SNAP_ACCOUNT })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SNAP_PERSON })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_ITIN })).not.toBeInTheDocument()
  })

  it('renders the full option set when session is unknown', () => {
    mockUseAuth.mockReturnValue({ session: null })

    renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SNAP_PERSON })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_ITIN })).not.toBeInTheDocument()
  })
})
