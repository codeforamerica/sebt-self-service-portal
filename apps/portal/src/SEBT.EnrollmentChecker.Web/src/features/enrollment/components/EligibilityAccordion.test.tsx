import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EligibilityAccordion } from './EligibilityAccordion'

const APPLY_HREF = 'https://apply.example.gov/'

// Thresholds are runtime configuration from the features endpoint, so the tests
// serve them the way the API would.
let mockIncomeEligibility: unknown = {
  baseThreshold: 28953,
  perMemberIncrement: 10175,
  maxHouseholdSize: 8
}

vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => ({ data: { incomeEligibility: mockIncomeEligibility } })
}))

async function expand(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button'))
}

describe('EligibilityAccordion', () => {
  beforeEach(() => {
    mockIncomeEligibility = {
      baseThreshold: 28953,
      perMemberIncrement: 10175,
      maxHouseholdSize: 8
    }
  })

  it('starts collapsed', () => {
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'false')
  })

  it('recomputes the income threshold as the household grows', async () => {
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    expect(screen.getByTestId('income-threshold')).toHaveTextContent('$28,953')

    await user.selectOptions(screen.getByTestId('household-size'), '4')
    expect(screen.getByTestId('income-threshold')).toHaveTextContent('$59,478')
  })

  it('offers exactly the configured number of household sizes', async () => {
    mockIncomeEligibility = { baseThreshold: 100, perMemberIncrement: 10, maxHouseholdSize: 3 }
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    expect(screen.getAllByRole('option')).toHaveLength(3)
  })

  it('screens against the configured figures, not built-in ones', async () => {
    mockIncomeEligibility = { baseThreshold: 30000, perMemberIncrement: 11000, maxHouseholdSize: 8 }
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    await user.selectOptions(screen.getByTestId('household-size'), '3')
    expect(screen.getByTestId('income-threshold')).toHaveTextContent('$52,000')
  })

  it('substitutes the authored figure rather than appending to it', async () => {
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    await user.selectOptions(screen.getByTestId('household-size'), '4')
    const text = screen.getByTestId('income-threshold').textContent ?? ''
    expect(text).not.toMatch(/[[\]]/)
    expect(text).not.toContain('$28,953')
  })

  it('announces the recomputed threshold', async () => {
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)
    expect(screen.getByTestId('income-threshold')).toHaveAttribute('aria-live', 'polite')
  })

  // Withdrawing the tool beats screening against figures we no longer have.
  it('withdraws the screening tool when no thresholds are configured', async () => {
    mockIncomeEligibility = null
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    expect(screen.queryByTestId('household-size')).toBeNull()
    expect(screen.queryByTestId('income-threshold')).toBeNull()
  })

  it('keeps the explanatory copy when the screening tool is withdrawn', async () => {
    mockIncomeEligibility = null
    const user = userEvent.setup()
    const { container } = render(<EligibilityAccordion applyHref={APPLY_HREF} />)
    await expand(user)

    expect(container).toHaveTextContent(/applyForSebtAccordionBody1|National School Lunch/i)
  })

  it('hides the apply link when no destination is configured', async () => {
    const user = userEvent.setup()
    render(<EligibilityAccordion applyHref={null} />)
    await expand(user)
    expect(screen.queryByTestId('accordion-apply-link')).toBeNull()
  })
})
