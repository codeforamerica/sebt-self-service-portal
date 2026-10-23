import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import coResult from '@/content/locales/en/co/result.json'
import { EnrollmentProvider } from '../context/EnrollmentContext'
import { ErrorResultPage } from './ErrorResultPage'

const portalUrl = 'https://portal.example.gov'

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }))

// Drives the season. A payload without `enrollment` is an open season, which is
// what these cases assume; the closed suite below sets it explicitly.
let mockFeatures: unknown = {}
vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => ({ data: mockFeatures })
}))

describe('ErrorResultPage', () => {
  it('renders the error title and body', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'Something went wrong on our end' })
    ).toBeInTheDocument()
    expect(screen.getByText(/You may try to check again later/)).toBeInTheDocument()
  })

  it('offers the portal step with the portal link', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(screen.getByText(/will receive their benefits/)).toBeInTheDocument()
    expect(screen.getByTestId('portal-link')).toHaveAttribute('href', portalUrl)
  })

  it('shows no application steps or links', () => {
    render(<ErrorResultPage portalUrl={portalUrl} />)

    expect(screen.queryByTestId('apply-2027-link')).toBeNull()
    expect(screen.queryByTestId('apply-for-sebt-link')).toBeNull()
    expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
  })
})

describe('ErrorResultPage — closed season', () => {
  beforeEach(() => {
    mockFeatures = { enrollment: { enabled: false } }
  })

  afterEach(() => {
    mockFeatures = {}
    vi.unstubAllEnvs()
  })

  // The card offers the next check, which reads flow state.
  const renderErrorPage = () =>
    render(
      <EnrollmentProvider>
        <ErrorResultPage portalUrl={portalUrl} />
      </EnrollmentProvider>
    )

  // The sheet writes a non-breaking space into this row, so compare on collapsed
  // whitespace rather than the literal string.
  const collapse = (value: string) => value.replace(/\s+/g, ' ').trim()

  it('explains the portal in the past tense', () => {
    renderErrorPage()

    expect(collapse(screen.getByTestId('portal-alert-link').textContent ?? '')).toBe(
      collapse(coResult.streamlinedEnrolledClosedAlertTitle)
    )
    expect(screen.queryByText(coResult.streamlinedEnrolledAlertTitle)).toBeNull()
  })

  it('makes the portal pointer a link rather than a heading', () => {
    renderErrorPage()
    expect(screen.getByTestId('portal-alert-link')).toHaveAttribute('href', portalUrl)
  })

  // A failed check in a closed season is otherwise a dead end: there is no
  // application to fall back on, so another check is the only move left.
  it('offers another check in a flow that checks one child at a time', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    renderErrorPage()
    expect(screen.getByTestId('check-another-child')).toBeInTheDocument()
  })

  // A flow with a review step collects the whole household before submitting, so
  // it has nothing to send the visitor back for.
  it('offers no further check in a review-step flow', () => {
    renderErrorPage()
    expect(screen.queryByTestId('check-another-child')).toBeNull()
  })
})
