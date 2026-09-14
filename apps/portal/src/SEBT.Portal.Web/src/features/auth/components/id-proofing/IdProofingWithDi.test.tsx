/**
 * Pins the DC id-proofing wiring: every user answers the SNAP/TANF question, whatever the
 * session says about co-loaded status, and the option arrays stay limited to the approved
 * values so removed entries (medicaidId, snapPersonId) can't quietly come back.
 */
import { DC_ID_OPTIONS, DC_SNAP_TANF_OPTION } from '@/app/(public)/login/id-proofing/dc-id-options'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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

const LABEL_SNAP_QUESTION = /Do you receive SNAP or TANF/
const INPUT_LABEL_CASE_NUMBER = /Enter your SNAP or TANF case number/
const LABEL_SSN = /Social Security Number \(SSN\)/
const LABEL_ITIN = /Individual Taxpayer ID Number \(ITIN\)/
const LABEL_NONE = /None of the above/

function renderComponent() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <IdProofingWithDi
        idOptions={DC_ID_OPTIONS}
        snapTanfOption={DC_SNAP_TANF_OPTION}
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
    expect(DC_ID_OPTIONS.map((o) => o.value)).toEqual(['ssn', 'itin', 'none'])
    expect(DC_SNAP_TANF_OPTION.value).toBe('snapAccountId')
    expect(DC_SNAP_TANF_OPTION.validation).toEqual({ digits: [7, 8] })
  })

  // Co-loaded status is only known after a SNAP/TANF match, so a first-time co-loaded user
  // arrives with isCoLoaded false. The question is how they identify themselves; the session
  // must not skip or reshape it.
  it.each([
    ['co-loaded', session(true)],
    ['not co-loaded', session(false)],
    ['unknown', null]
  ])('asks the SNAP/TANF question when the session is %s', async (_, currentSession) => {
    mockUseAuth.mockReturnValue({ session: currentSession })
    const user = userEvent.setup()

    const { container } = renderComponent()

    expect(screen.getByRole('group', { name: LABEL_SNAP_QUESTION })).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: LABEL_SSN })).not.toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'Yes' }))
    expect(screen.getByRole('textbox', { name: INPUT_LABEL_CASE_NUMBER })).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'No' }))
    expect(screen.queryByRole('textbox', { name: INPUT_LABEL_CASE_NUMBER })).not.toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
    expect(container.querySelector('hr')).toBeInTheDocument()
  })
})
