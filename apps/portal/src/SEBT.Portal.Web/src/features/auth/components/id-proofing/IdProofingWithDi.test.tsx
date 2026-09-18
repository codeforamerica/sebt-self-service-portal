/**
 * Covers the option-set switching logic: co-loaded users see the narrower
 * co-loaded set; everyone else sees the full option list. With
 * enable_socure_snap_tanf_question on, every user answers the SNAP/TANF question
 * instead. Also pins the production DC option arrays so removed entries
 * (medicaidId, snapPersonId) can't quietly come back.
 */
import {
  DC_ID_OPTIONS,
  DC_ID_OPTIONS_AFTER_NO,
  DC_ID_OPTIONS_CO_LOADED,
  DC_SNAP_TANF_OPTION
} from '@/app/(public)/login/id-proofing/dc-id-options'
import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
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

// Helper-text decorated options (snapAccountId) extend the radio's accessible
// name beyond the bold label, so we match on a regex anchored to the bold label.
const LABEL_SSN = /Social Security Number \(SSN\)/
const LABEL_ITIN = /Individual Taxpayer ID Number \(ITIN\)/
const LABEL_SNAP_ACCOUNT = /SNAP or TANF account ID/
const LABEL_SNAP_PERSON = /SNAP or TANF person ID/
const LABEL_MEDICAID = /^Medicaid ID/
const LABEL_NONE = /None of the above/
const LABEL_SNAP_QUESTION = /Do you receive SNAP or TANF/
const INPUT_LABEL_CASE_NUMBER = /Enter your SNAP or TANF case number/

function flagsContext(overrides: Partial<FeatureFlagsContextValue>): FeatureFlagsContextValue {
  return {
    flags: { enable_socure_snap_tanf_question: true },
    isLoading: false,
    isError: false,
    ...overrides
  }
}

function renderComponent(flags?: FeatureFlagsContextValue) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  })
  const component = (
    <IdProofingWithDi
      idOptions={DC_ID_OPTIONS}
      coLoadedIdOptions={DC_ID_OPTIONS_CO_LOADED}
      snapTanfIdOptions={DC_ID_OPTIONS_AFTER_NO}
      snapTanfOption={DC_SNAP_TANF_OPTION}
      contactLink={TEST_CONTACT_LINK}
    />
  )
  return render(
    <QueryClientProvider client={queryClient}>
      {flags ? (
        <FeatureFlagsContext.Provider value={flags}>{component}</FeatureFlagsContext.Provider>
      ) : (
        component
      )}
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
    expect(DC_ID_OPTIONS_AFTER_NO.map((o) => o.value)).toEqual(['ssn', 'itin', 'none'])
    expect(DC_SNAP_TANF_OPTION.value).toBe('snapAccountId')
    expect(DC_SNAP_TANF_OPTION.validation).toEqual({ digits: [7, 8] })
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

  describe('with enable_socure_snap_tanf_question on', () => {
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

      const { container } = renderComponent(flagsContext({}))

      expect(screen.getByRole('group', { name: LABEL_SNAP_QUESTION })).toBeInTheDocument()
      expect(screen.queryByRole('radio', { name: LABEL_SSN })).not.toBeInTheDocument()

      await user.click(screen.getByRole('radio', { name: 'Yes' }))
      expect(screen.getByRole('textbox', { name: INPUT_LABEL_CASE_NUMBER })).toBeInTheDocument()

      await user.click(screen.getByRole('radio', { name: 'No' }))
      expect(
        screen.queryByRole('textbox', { name: INPUT_LABEL_CASE_NUMBER })
      ).not.toBeInTheDocument()
      expect(screen.getByRole('radio', { name: LABEL_SSN })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: LABEL_ITIN })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: LABEL_NONE })).toBeInTheDocument()
      expect(screen.queryByRole('radio', { name: LABEL_SNAP_ACCOUNT })).not.toBeInTheDocument()
      expect(container.querySelector('hr')).toBeInTheDocument()
    })

    it('holds the form while feature flags are still loading', () => {
      mockUseAuth.mockReturnValue({ session: session(false) })

      renderComponent(flagsContext({ flags: {}, isLoading: true }))

      expect(screen.queryByRole('group', { name: LABEL_SNAP_QUESTION })).not.toBeInTheDocument()
      expect(screen.queryByRole('radio', { name: LABEL_SSN })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /continue/i })).not.toBeInTheDocument()
    })
  })
})
