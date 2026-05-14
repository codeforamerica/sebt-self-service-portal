import { i18n } from '@sebt/design-system/client'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { IncomeCalculator } from './IncomeCalculator'

describe('IncomeCalculator', () => {
  let user: ReturnType<typeof userEvent.setup>

  beforeEach(() => {
    user = userEvent.setup()
  })

  afterEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('renders the helper paragraph above the dropdown', () => {
    render(<IncomeCalculator />)
    expect(
      screen.getByText(i18n.t('applyForSebtAccordionBody3', { ns: 'result' }))
    ).toBeInTheDocument()
  })

  it('renders the dropdown with options 1-20 and defaults to 1', () => {
    render(<IncomeCalculator />)
    const select = screen.getByRole('combobox', {
      name: 'Select the number of people in your household',
    })
    const options = within(select).getAllByRole('option') as HTMLOptionElement[]
    expect(options).toHaveLength(20)
    expect(options[0].value).toBe('1')
    expect(options[19].value).toBe('20')
    expect((select as HTMLSelectElement).value).toBe('1')
  })

  it('renders the alert with the threshold for size 1 by default', () => {
    render(<IncomeCalculator />)
    const alert = screen.getByRole('status')
    expect(alert).toHaveTextContent('$28,953')
  })

  it('updates the alert threshold when household size changes to 4', async () => {
    render(<IncomeCalculator />)
    const select = screen.getByRole('combobox', {
      name: 'Select the number of people in your household',
    })
    await user.selectOptions(select, '4')
    expect(screen.getByRole('status')).toHaveTextContent('$59,478')
  })

  it('updates the alert threshold when household size changes to 12', async () => {
    render(<IncomeCalculator />)
    const select = screen.getByRole('combobox', {
      name: 'Select the number of people in your household',
    })
    await user.selectOptions(select, '12')
    expect(screen.getByRole('status')).toHaveTextContent('$140,878')
  })

  it('updates the alert threshold when household size changes to 20', async () => {
    render(<IncomeCalculator />)
    const select = screen.getByRole('combobox', {
      name: 'Select the number of people in your household',
    })
    await user.selectOptions(select, '20')
    expect(screen.getByRole('status')).toHaveTextContent('$222,278')
  })

  it('exposes the alert via role="status" so screen readers announce updates', () => {
    render(<IncomeCalculator />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders with a stable data-testid for ResultsPage integration', () => {
    render(<IncomeCalculator />)
    expect(screen.getByTestId('income-calculator')).toBeInTheDocument()
  })

  it('renders the Spanish alert template with the en-US formatted threshold', async () => {
    await i18n.changeLanguage('es')
    render(<IncomeCalculator />)
    const select = screen.getByRole('combobox', {
      name: 'Selecciona el número de personas que viven en tu hogar',
    })
    await user.selectOptions(select, '4')
    const alert = screen.getByRole('status')
    expect(alert).toHaveTextContent('Si el ingreso total de tu hogar es menos de')
    expect(alert).toHaveTextContent('$59,478')
  })
})
