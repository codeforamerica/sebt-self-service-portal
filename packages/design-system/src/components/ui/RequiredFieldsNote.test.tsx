import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { RequiredFieldsNote } from './RequiredFieldsNote'

describe('RequiredFieldsNote', () => {
  it('renders the full note text', () => {
    const { container } = render(<RequiredFieldsNote text="Asterisks (*) indicate a required field." />)

    expect(container.querySelector('p')).toHaveTextContent('Asterisks (*) indicate a required field.')
  })

  it('renders the asterisk in the error color', () => {
    render(<RequiredFieldsNote text="Asterisks (*) indicate a required field." />)

    expect(screen.getByText('*')).toHaveClass('text-error')
  })

  it('does not render the note as hint text', () => {
    const { container } = render(<RequiredFieldsNote text="Asterisks (*) indicate a required field." />)

    expect(container.querySelector('p')).not.toHaveClass('usa-hint')
  })

  it('renders text without an asterisk unchanged', () => {
    const { container } = render(<RequiredFieldsNote text="All fields are required." />)

    expect(container.querySelector('p')).toHaveTextContent('All fields are required.')
    expect(container.querySelector('.text-error')).toBeNull()
  })

  it('applies the given className', () => {
    const { container } = render(
      <RequiredFieldsNote
        text="Asterisks (*) indicate a required field."
        className="margin-bottom-3"
      />
    )

    expect(container.querySelector('p')).toHaveClass('margin-bottom-3')
  })
})
