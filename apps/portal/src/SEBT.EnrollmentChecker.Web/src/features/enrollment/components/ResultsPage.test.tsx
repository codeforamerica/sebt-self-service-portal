import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import coResult from '@/content/locales/en/co/result.json'
import { EnrollmentProvider } from '../context/EnrollmentContext'
import { ResultsPage } from './ResultsPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

// These tests cover results composition. Applications are open so the apply
// blocks render; mockApplyHref below is what closes them. Income screening and
// the apply flag are exercised in their own suites. Omitting `enrollment` leaves
// the season open, which is what every suite but the closed one assumes.
const OPEN_SEASON_FEATURES = { apply: { enabled: true } }
let mockFeatures: unknown = OPEN_SEASON_FEATURES
vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => ({ data: mockFeatures })
}))

// Flows with a review step collect the household before submitting, so they
// have no reason to send the visitor back for another child.
describe('sequential checks', () => {
  it('offers no check-another card in a review-step flow', () => {
    render(<ResultsPage results={mixedEnrolled} portalUrl="https://portal.example.gov" />)
    expect(screen.queryByTestId('check-another-child')).not.toBeInTheDocument()
  })
})

let mockApplyHref: string | null = 'https://apply.example.gov/?language=en_US'
vi.mock('@/lib/applyHref', () => ({
  getApplyHref: () => mockApplyHref
}))

const mixedEnrolled: ChildCheckApiResponse[] = [
  {
    checkId: '1',
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12',
    status: 'Match'
  },
  {
    checkId: '2',
    firstName: 'John',
    lastName: 'Smith',
    dateOfBirth: '2016-01-01',
    status: 'NonMatch'
  },
  {
    checkId: '3',
    firstName: 'Alex',
    lastName: 'Lee',
    dateOfBirth: '2014-05-05',
    status: 'Error',
    statusMessage: 'Service error'
  },
  {
    checkId: '4',
    firstName: 'Jimbo',
    lastName: 'Smith',
    dateOfBirth: '2008-01-01',
    status: 'NonMatch'
  }
]

const allEnrolled: ChildCheckApiResponse[] = [
  {
    checkId: '1',
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12',
    status: 'Match'
  },
  {
    checkId: '2',
    firstName: 'Sally',
    lastName: 'Smith',
    dateOfBirth: '2016-01-01',
    status: 'Match'
  }
]

const noneEnrolled: ChildCheckApiResponse[] = [
  {
    checkId: '1',
    firstName: 'Jane',
    lastName: 'Doe',
    dateOfBirth: '2015-04-12',
    status: 'NonMatch'
  },
  {
    checkId: '2',
    firstName: 'Sally',
    lastName: 'Wetherbee',
    dateOfBirth: '2016-01-01',
    status: 'NonMatch'
  }
]

const errorResponse: ChildCheckApiResponse[] = [
  {
    checkId: '3',
    firstName: 'Alex',
    lastName: 'Lee',
    dateOfBirth: '2014-05-05',
    status: 'Error',
    statusMessage: 'Service error'
  }
]

// Copy anchors from the CO 2026-closed content (DC-701 designs).
const enrolledBoxTitle = 'were enrolled in Summer EBT for 2026'
const notEnrolledTitle = 'were NOT enrolled'
const closedLine = 'Enrollment in Summer EBT for 2026 is now closed.'
const apply2027LinkText = /considered for benefits for summer 2027/
const nextStepsSectionText = 'Next steps'
const portalUrl = 'https://portal.example.gov'

describe('ResultsPage', () => {
  beforeEach(() => {
    mockApplyHref = 'https://apply.example.gov/?language=en_US'
  })

  describe('Mixed enrollment household', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={mixedEnrolled}
          portalUrl={portalUrl}
        />
      )
    })

    it('shows enrolled children in the summary box', () => {
      const enrolledBox = screen.getByTestId('enrolled-summary-box')
      expect(enrolledBox).toHaveTextContent(enrolledBoxTitle)
      expect(enrolledBox).toHaveTextContent('Jane Doe')
      expect(enrolledBox).not.toHaveTextContent('John Smith')
      expect(enrolledBox).not.toHaveTextContent('Jimbo Smith')
    })

    it('lists not-enrolled children below the box, outside any summary box', () => {
      const inline = screen.getByTestId('not-enrolled-inline')
      expect(inline).toHaveTextContent(notEnrolledTitle)
      expect(inline).toHaveTextContent('John Smith')
      expect(inline).toHaveTextContent('Jimbo Smith')
      expect(inline).not.toHaveTextContent('Jane Doe')
      expect(screen.queryByTestId('not-enrolled-summary-box')).toBeNull()
    })

    it('orders next steps portal-first, then the 2027 application step', () => {
      expect(screen.getByText(nextStepsSectionText)).toBeVisible()
      const steps = screen.getAllByTestId(/next-step-/)
      expect(steps[0]).toHaveAttribute('data-testid', 'next-step-portal')
      expect(steps[1]).toHaveAttribute('data-testid', 'next-step-apply-2027')
    })

    it('shows the portal step with the heading and portal link', () => {
      const portalStep = screen.getByTestId('next-step-portal')
      expect(portalStep).toHaveTextContent('will receive their benefits')
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveAttribute('href', portalUrl)
    })

    it('shows the 2027 application step with closure copy, link, and wait note', () => {
      const applyStep = screen.getByTestId('next-step-apply-2027')
      expect(applyStep).toHaveTextContent('Submit a 2027 Summer EBT application')
      expect(applyStep).toHaveTextContent(
        'didn’t have enough information to determine their eligibility'
      )
      expect(applyStep).toHaveTextContent(closedLine)
      const applyLink = screen.getByTestId('apply-2027-link')
      expect(applyLink).toHaveAttribute('href', mockApplyHref)
      expect(applyStep).toHaveTextContent('You will not hear back about your application')
    })

    it('renders no eligibility accordion and no income calculator', () => {
      expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
      expect(screen.queryByTestId('income-calculator')).toBeNull()
    })
  })

  describe('All children enrolled', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={allEnrolled}
          portalUrl={portalUrl}
        />
      )
    })

    it('shows all children in the enrolled summary box', () => {
      const enrolledBox = screen.getByTestId('enrolled-summary-box')
      expect(enrolledBox).toHaveTextContent(enrolledBoxTitle)
      expect(enrolledBox).toHaveTextContent('Jane Doe')
      expect(enrolledBox).toHaveTextContent('Sally Smith')
    })

    it('does not render a not-enrolled section', () => {
      expect(screen.queryByTestId('not-enrolled-summary-box')).toBeNull()
      expect(screen.queryByTestId('not-enrolled-inline')).toBeNull()
    })

    it('shows the portal link and no apply link', () => {
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveAttribute('href', portalUrl)
      expect(screen.queryByTestId('apply-2027-link')).toBeNull()
    })

    it('renders no accordion, calculator, or numbered next steps', () => {
      expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
      expect(screen.queryByTestId('income-calculator')).toBeNull()
      expect(screen.queryByText(nextStepsSectionText)).toBeNull()
    })
  })

  describe('No children enrolled', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={noneEnrolled}
          portalUrl={portalUrl}
        />
      )
    })

    it('shows all children in the not-enrolled summary box', () => {
      const notEnrolledBox = screen.getByTestId('not-enrolled-summary-box')
      expect(notEnrolledBox).toHaveTextContent(notEnrolledTitle)
      expect(notEnrolledBox).toHaveTextContent('Jane Doe')
      expect(notEnrolledBox).toHaveTextContent('Sally Wetherbee')
    })

    it('does not render an enrolled section', () => {
      expect(screen.queryByTestId('enrolled-summary-box')).toBeNull()
    })

    it('shows the closure line and the 2027 application link with wait note', () => {
      expect(screen.getByText(closedLine)).toBeVisible()
      const applyLink = screen.getByTestId('apply-2027-link')
      expect(applyLink).toHaveTextContent(apply2027LinkText)
      expect(applyLink).toHaveAttribute('href', mockApplyHref)
      expect(screen.getByText(/hear back about your application in summer 2027/)).toBeVisible()
    })

    it('does not link to the portal', () => {
      expect(screen.queryByTestId('portal-link')).toBeNull()
    })

    it('renders no accordion, calculator, or apply-online button', () => {
      expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
      expect(screen.queryByTestId('income-calculator')).toBeNull()
      expect(screen.queryByTestId('apply-for-sebt-link')).toBeNull()
    })
  })

  describe('Graceful degradation without an application URL', () => {
    it('keeps the closure line but hides the 2027 link and wait note', () => {
      mockApplyHref = null
      render(
        <ResultsPage
          results={noneEnrolled}
          portalUrl={portalUrl}
        />
      )

      expect(screen.getByText(closedLine)).toBeVisible()
      expect(screen.queryByTestId('apply-2027-link')).toBeNull()
      expect(screen.queryByText(/hear back about your application in summer 2027/)).toBeNull()
    })

    it('mixed household: drops the numbered steps, keeps the portal section and the not-enrolled explanation', () => {
      mockApplyHref = null
      render(
        <ResultsPage
          results={mixedEnrolled}
          portalUrl={portalUrl}
        />
      )

      // Only one actionable step remains, so the numbered list and the
      // imperative "Submit a 2027 application" step go away together.
      expect(screen.queryByText(nextStepsSectionText)).toBeNull()
      expect(screen.queryByTestId('next-step-apply-2027')).toBeNull()
      expect(screen.queryByText(/Submit a 2027 Summer EBT application/)).toBeNull()
      expect(screen.queryByTestId('apply-2027-link')).toBeNull()
      expect(screen.queryByText(/hear back about your application/)).toBeNull()

      // The not-enrolled list and the portal step (as a plain section, like the
      // all-enrolled page) both survive.
      expect(screen.getByTestId('not-enrolled-inline')).toBeInTheDocument()
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveAttribute('href', portalUrl)

      // Not-enrolled children still get the explanation and the closure line,
      // placed after the portal step to keep the designed portal-first order.
      expect(
        screen.getByText(/didn’t have enough information to determine their eligibility/)
      ).toBeVisible()
      const closure = screen.getByText(closedLine)
      expect(closure).toBeVisible()
      expect(
        portalLink.compareDocumentPosition(closure) & Node.DOCUMENT_POSITION_FOLLOWING
      ).toBeTruthy()
    })
  })

  // enable_apply covers this season's window. The 2027 link is next season's
  // application, offered because this season closed, so the flag going off is
  // exactly when it matters most.
  describe('With this season’s applications closed', () => {
    beforeEach(() => {
      mockFeatures = { apply: { enabled: false } }
    })

    afterEach(() => {
      mockFeatures = OPEN_SEASON_FEATURES
    })

    it('still offers the 2027 link on the no-results page', () => {
      render(
        <ResultsPage
          results={noneEnrolled}
          portalUrl={portalUrl}
        />
      )

      expect(screen.getByTestId('apply-2027-link')).toHaveAttribute('href', mockApplyHref)
    })

    it('still offers the numbered 2027 step on a mixed household', () => {
      render(
        <ResultsPage
          results={mixedEnrolled}
          portalUrl={portalUrl}
        />
      )

      expect(screen.getByText(nextStepsSectionText)).toBeVisible()
      expect(screen.getByTestId('next-step-apply-2027')).toBeVisible()
      expect(screen.getByTestId('apply-2027-link')).toBeVisible()
    })
  })

  describe('Indeterminate results (No Results shape)', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={errorResponse}
          portalUrl={portalUrl}
        />
      )
    })

    it('lists the children in the not-enough-information summary box', () => {
      const noInfoBox = screen.getByTestId('no-info-summary-box')
      expect(noInfoBox).toHaveTextContent(
        "We don't have enough information for the following children."
      )
      expect(noInfoBox).toHaveTextContent('Alex Lee')
    })

    it('explains the no-info outcome and offers the 2027 application link', () => {
      expect(
        screen.getByText(/didn’t have enough information to determine their eligibility/)
      ).toBeVisible()
      expect(screen.getByText(closedLine)).toBeVisible()
      expect(screen.getByTestId('apply-2027-link')).toHaveAttribute('href', mockApplyHref)
    })

    it('renders no numbered next steps, accordion, or calculator', () => {
      expect(screen.queryByText(nextStepsSectionText)).toBeNull()
      expect(screen.queryByTestId('eligibility-accordion')).toBeNull()
      expect(screen.queryByTestId('income-calculator')).toBeNull()
    })
  })
})

// A single-outcome flow answers for one child, so the outcome is the heading
// and there is nobody to name below it.
//
// i18n initialises once at import with one state's resources (see
// vitest.config.ts), so DC keys do not resolve here and i18next echoes the key
// back. That makes the rendered heading the exact key the component chose,
// which is the selection this suite is testing; the copy itself comes from the
// content pipeline.
describe('single-outcome results', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  const oneChild = (status: string): ChildCheckApiResponse[] => [
    { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status }
  ]

  // The single-outcome results offer the next check, which reads flow state.
  const renderResults = (status: string) =>
    render(
      <EnrollmentProvider>
        <ResultsPage results={oneChild(status)} portalUrl="https://portal.example.gov" />
      </EnrollmentProvider>
    )

  const heading = () => screen.getByRole('heading', { level: 1 }).textContent

  it('makes the enrolled outcome the heading', () => {
    renderResults('Match')
    expect(heading()).toBe('streamlinedEnrolledTitle')
  })

  it('makes the not-enrolled outcome the heading', () => {
    renderResults('NonMatch')
    expect(heading()).toBe('applyForSebtTitle')
  })

  it('folds an unresolved check into the not-enrolled outcome', () => {
    renderResults('Error')
    expect(heading()).toBe('applyForSebtTitle')
  })

  it('names no children and renders no numbered next steps', () => {
    const { container } = renderResults('Match')
    expect(screen.queryByText(/Jane Doe/)).toBeNull()
    expect(container.querySelector('.usa-process-list')).toBeNull()
  })

  it('offers the next check', () => {
    renderResults('Match')
    expect(screen.getByTestId('check-another-child')).toBeInTheDocument()
  })

  // The portal guidance is a success alert in the design, and the button that
  // acts on it sits outside so the alert stays informational.
  it('presents the portal guidance as a success alert', () => {
    const { container } = renderResults('Match')
    const alert = container.querySelector('.usa-alert')

    expect(alert).toHaveClass('usa-alert--success')
    expect(alert?.querySelector('.usa-alert__heading')).toBeInTheDocument()
    expect(alert?.contains(screen.getByTestId('portal-link'))).toBe(false)
  })

  // role="alert" is an assertive live region; this is static page content.
  it('does not announce the alert as a live region', () => {
    const { container } = renderResults('Match')
    expect(container.querySelector('.usa-alert')).not.toHaveAttribute('role', 'alert')
  })
})

// Once the season has closed the check still runs, but it reports on a season
// that is over: past-tense copy, and no application to send anyone to.
describe('single-outcome results — closed season', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    mockFeatures = { apply: { enabled: true }, enrollment: { enabled: false } }
  })

  afterEach(() => {
    vi.unstubAllEnvs()
    mockFeatures = OPEN_SEASON_FEATURES
  })

  const oneChild = (status: string): ChildCheckApiResponse[] => [
    { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status }
  ]

  const renderResults = (status: string) =>
    render(
      <EnrollmentProvider>
        <ResultsPage results={oneChild(status)} portalUrl="https://portal.example.gov" />
      </EnrollmentProvider>
    )

  const heading = () => screen.getByRole('heading', { level: 1 }).textContent

  it('reports the enrolled outcome in the past tense', () => {
    renderResults('Match')
    expect(heading()).toBe('streamlinedEnrolledClosedTitle')
  })

  it('reports the not-enrolled outcome in the past tense', () => {
    renderResults('NonMatch')
    expect(heading()).toBe('applyForSebtClosedTitle')
  })

  // The heading is the whole answer: there is no application left to explain and
  // no eligibility left to screen for, so nothing follows it but the next check.
  it('answers a not-enrolled check with the heading alone', () => {
    renderResults('NonMatch')

    expect(screen.queryByTestId('application-available')).toBeNull()
    expect(screen.queryByRole('button', { name: /applyForSebtAccordionTitle/ })).toBeNull()
    expect(screen.queryByTestId('accordion-apply-link')).toBeNull()
    expect(screen.queryByTestId('apply-online-link')).toBeNull()
  })

  // Applications being open cannot resurrect an apply path after the season ends.
  it('drops the apply paths even while the apply flag is on', () => {
    renderResults('NonMatch')
    expect(screen.queryByTestId('apply-online-link')).toBeNull()
    expect(screen.queryByTestId('accordion-apply-link')).toBeNull()
  })

  it('still offers the next check, in the season’s wording', () => {
    renderResults('NonMatch')

    expect(screen.getByTestId('check-another-child')).toBeInTheDocument()
    expect(screen.getByText('applyForSebtClosedCard2Body')).toBeInTheDocument()
  })

  // An alert marks something to act on. A closed season's enrolled result is a
  // record of where benefits already went, so the portal pointer is a plain link.
  it('drops the success alert and leads with the portal link', () => {
    const { container } = renderResults('Match')

    expect(container.querySelector('.usa-alert')).toBeNull()
    expect(screen.getByTestId('portal-alert-link')).toHaveAttribute(
      'href',
      'https://portal.example.gov'
    )
    // CO ships this row, so it resolves rather than echoing its key. Compare on
    // collapsed whitespace: the sheet writes a non-breaking space into the copy.
    const collapse = (value: string) => value.replace(/\s+/g, ' ').trim()
    expect(collapse(screen.getByTestId('portal-alert-link').textContent ?? '')).toBe(
      collapse(coResult.streamlinedEnrolledClosedAlertTitle)
    )
  })

  it('keeps the portal button on the enrolled result', () => {
    renderResults('Match')
    expect(screen.getByTestId('portal-link')).toHaveAttribute(
      'href',
      'https://portal.example.gov'
    )
  })

  it('explains the enrolled outcome in the past tense', () => {
    renderResults('Match')
    expect(screen.getByText('streamlinedEnrolledClosedBody')).toBeInTheDocument()
  })
})
