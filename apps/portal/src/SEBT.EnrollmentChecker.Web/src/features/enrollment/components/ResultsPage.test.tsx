import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ResultsPage } from './ResultsPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

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

    it('shows the portal step with the expiration-aware heading and portal link', () => {
      const portalStep = screen.getByTestId('next-step-portal')
      expect(portalStep).toHaveTextContent('received their benefits and when they expire')
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveAttribute('href', portalUrl)
    })

    it('shows the 2027 application step with closure copy, link, and wait note', () => {
      const applyStep = screen.getByTestId('next-step-apply-2027')
      expect(applyStep).toHaveTextContent('Submit a 2027 Summer EBT application')
      expect(applyStep).toHaveTextContent(
        "didn't have enough information to determine their eligibility"
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
        screen.getByText(/didn't have enough information to determine their eligibility/)
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
