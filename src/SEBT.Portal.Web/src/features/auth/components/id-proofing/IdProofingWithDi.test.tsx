/**
 * Covers the option-set switching logic: co-loaded users see the narrower
 * co-loaded set; everyone else sees the full option list. Also pins the
 * production DC option arrays so removed entries (medicaidId, snapPersonId)
 * can't quietly come back.
 */
import {
  DC_ID_OPTIONS,
  DC_ID_OPTIONS_CO_LOADED
} from '@/app/(public)/login/id-proofing/dc-id-options'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import type { SessionInfo } from '../../context'
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

// Helper-text decorated options (snapAccountId) extend the radio's accessible
// name beyond the bold label, so we match on a regex anchored to the bold label.
const LABEL_SSN = /Social Security Number \(SSN\)/
const LABEL_ITIN = /Individual Taxpayer ID Number \(ITIN\)/
const LABEL_SNAP_ACCOUNT = /SNAP or TANF account ID/
const LABEL_SNAP_PERSON = /SNAP or TANF person ID/
const LABEL_MEDICAID = /^Medicaid ID/
const LABEL_NONE = /None of the above/

function renderComponent() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <IdProofingWithDi
        idOptions={DC_ID_OPTIONS}
        coLoadedIdOptions={DC_ID_OPTIONS_CO_LOADED}
        contactLink={TEST_CONTACT_LINK}
      />
    </QueryClientProvider>
  )
}

function session(isCoLoaded: boolean): SessionInfo {
  return {
    userId: null,
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
  it('exposes only the approved DC ID option values (regression guard)', () => {
    expect(DC_ID_OPTIONS.map((o) => o.value)).toEqual(['ssn', 'itin', 'snapAccountId', 'none'])
    expect(DC_ID_OPTIONS_CO_LOADED.map((o) => o.value)).toEqual(['snapAccountId', 'itin', 'none'])
  })

  it('renders the co-loaded option set with a divider before "None"', () => {
    mockUseAuth.mockReturnValue({ session: session(true) })

    const { container } = renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SNAP_ACCOUNT })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SSN })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SNAP_PERSON })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_MEDICAID })).not.toBeInTheDocument()
    expect(container.querySelector('hr')).toBeInTheDocument()
  })

  it('renders the full option set when session.isCoLoaded is false', () => {
    mockUseAuth.mockReturnValue({ session: session(false) })

    const { container } = renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SNAP_ACCOUNT })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SNAP_PERSON })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_MEDICAID })).not.toBeInTheDocument()
    expect(container.querySelector('hr')).toBeInTheDocument()
  })

  it('renders the full option set when session is unknown', () => {
    mockUseAuth.mockReturnValue({ session: null })

    renderComponent()

    expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SNAP_ACCOUNT })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SNAP_PERSON })).not.toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_MEDICAID })).not.toBeInTheDocument()
  })
})
