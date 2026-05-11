import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ResultsPage } from './ResultsPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

const mockApplyHref = 'https://apply.example.gov/?language=en_US'
vi.mock('@/lib/applyHref', () => ({
  getApplyHref: () => mockApplyHref
}))

const mixedEnrolled: ChildCheckApiResponse[] = [
  { 
    checkId: '1', 
    firstName: 'Jane', 
    lastName: 'Doe', 
    dateOfBirth: '2015-04-12', 
    status: 'Match' },
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
  { checkId: '1',
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
    firstName: '',
    lastName: '',
    dateOfBirth: '2014-05-05',
    status: 'Error',
    statusMessage: 'Service error'
  }
]

const eligibilityAccordionText = 'How do I know if'
const enrolledSectionText = 'already enrolled'
const notEnrolledSectionText = 'NOT enrolled'
const nextStepsSectionText = 'Next Steps'
const portalUrl = 'https://portal.example.gov'

describe('ResultsPage', () => {
  describe('Mixed enrollment household', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={mixedEnrolled}
          portalUrl={portalUrl}
        />
      )
    })

    it('shows enrolled children only in enrolled section', () => {
      const enrolledBox = screen.getByTestId('enrolled-summary-box')
      expect(enrolledBox).toHaveTextContent(enrolledSectionText)
      expect(enrolledBox).toHaveTextContent('Jane Doe')
      expect(enrolledBox).not.toHaveTextContent('John Smith')
      expect(enrolledBox).not.toHaveTextContent('Jimbo Smith')
    })

    it('shows not-enrolled children only in not-enrolled section', () => {
      const notEnrolledBox = screen.getByTestId('not-enrolled-summary-box')
      expect(notEnrolledBox).toHaveTextContent(notEnrolledSectionText)
      expect(notEnrolledBox).toHaveTextContent('John Smith')
      expect(notEnrolledBox).toHaveTextContent('Jimbo Smith')
      expect(notEnrolledBox).not.toHaveTextContent('Jane Doe')
    })

    it('shows next steps list', () => {
      expect(screen.getByText(nextStepsSectionText)).toBeVisible()
    })

    it('shows link to apply for sebt', () => {
      const applyLink = screen.getByTestId('apply-for-sebt-link')
      expect(applyLink).toBeVisible()
      expect(applyLink).toHaveAttribute('href', mockApplyHref)
    })

    it('shows link to log into sebt portal', () => {
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveTextContent('Summer EBT Portal')
      expect(portalLink).toHaveAttribute('href', portalUrl)
    })

    it('shows eligibility accordion', () => {
      const accordion = screen.getByTestId('eligibility-accordion')
      expect(accordion).toHaveTextContent(eligibilityAccordionText)
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

    it('shows all children in enrolled section', () => {
      const enrolledBox = screen.getByTestId('enrolled-summary-box')
      expect(enrolledBox).toHaveTextContent(enrolledSectionText)

      expect(enrolledBox).toHaveTextContent('Jane Doe')
      expect(enrolledBox).toHaveTextContent('Sally Smith')
    })

    it('does not render non-enrolled section', () => {
      expect(screen.queryByTestId('not-enrolled-summary-box')).toBeNull()
    })

    it('shows link to log into sebt portal', () => {
      const portalLink = screen.getByTestId('portal-link')
      expect(portalLink).toHaveTextContent('Summer EBT Portal')
      expect(portalLink).toHaveAttribute('href', portalUrl)
    })

    it('does not show link to apply', () => {
      expect(screen.queryByTestId('apply-for-sebt-link')).toBeNull()
    })

    it('does not show eligibility accordion', () => {
      expect(screen).not.toContain(eligibilityAccordionText)
    })

    it('does not contain next steps', () => {
      expect(screen).not.toContain(nextStepsSectionText)
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
    it('shows all children in non-enrolled section', () => {
      const notEnrolledBox = screen.getByTestId('not-enrolled-summary-box')
      expect(notEnrolledBox).toHaveTextContent(notEnrolledSectionText)

      expect(notEnrolledBox).toHaveTextContent('Jane Doe')
      expect(notEnrolledBox).toHaveTextContent('Sally Wetherbee')
    })

    it('does not render enrolled section', () => {
      expect(screen.queryByTestId('enrolled-summary-box')).toBeNull()
    })

    it('shows link to apply for sebt', () => {
      const applyLink = screen.getByTestId('apply-for-sebt-link')
      expect(applyLink).toBeVisible()
      expect(applyLink).toHaveAttribute('href', mockApplyHref)
    })

    it('does not link to portal', () => {
      expect(screen.queryByTestId('portal-link')).toBeNull()
    })

    it('shows eligibility accordion', () => {
      const accordion = screen.getByTestId('eligibility-accordion')
      expect(accordion).toHaveTextContent(eligibilityAccordionText)
    })
  })

  describe('Error or indeterminate state', () => {
    beforeEach(() => {
      render(
        <ResultsPage
          results={errorResponse}
          portalUrl={portalUrl}
        />
      )
    })
    it('shows error child with error message', () => {
      expect(screen.getByText(/Service error/i)).toBeInTheDocument()
    })
    it('shows next steps list', () => {
      expect(screen.getByText(nextStepsSectionText)).toBeVisible()
    })
  })

  describe('IncomeCalculator rendering', () => {
    it('renders IncomeCalculator on mixedEnrolled branch', () => {
      render(
        <ResultsPage
          results={mixedEnrolled}
          portalUrl={portalUrl}
        />
      )
      expect(screen.getByTestId('income-calculator')).toBeInTheDocument()
    })

    it('renders IncomeCalculator on noneEnrolled branch', () => {
      render(
        <ResultsPage
          results={noneEnrolled}
          portalUrl={portalUrl}
        />
      )
      expect(screen.getByTestId('income-calculator')).toBeInTheDocument()
    })

    it('does NOT render IncomeCalculator on allEnrolled branch', () => {
      render(
        <ResultsPage
          results={allEnrolled}
          portalUrl={portalUrl}
        />
      )
      expect(screen.queryByTestId('income-calculator')).toBeNull()
    })

    it('renders IncomeCalculator on indeterminate branch (inherited behavior)', () => {
      // indeterminate: no enrolled, no notEnrolled — all results are error
      render(
        <ResultsPage
          results={errorResponse}
          portalUrl={portalUrl}
        />
      )
      expect(screen.getByTestId('income-calculator')).toBeInTheDocument()
    })
  })
})
