import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { EnrolledSection } from './EnrolledSection'

const enrolled: ChildCheckApiResponse[] = [
  { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' },
  { checkId: '2', firstName: 'Betrand', lastName: 'Doe', dateOfBirth: '2014-01-13', status: 'Match' }
]

describe('EnrolledSection', () => {
  it('renders the header copy', () => {
    render(<EnrolledSection results={enrolled} />)
    expect(screen.getByText(/already enrolled/i))
  })

  it('renders enrolled child names', () => {
    render(<EnrolledSection results={enrolled} />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
    expect(screen.getByText(/Betrand Doe/i)).toBeInTheDocument()
  })

  it('renders nothing when empty', () => {
    const { container } = render(<EnrolledSection results={[]} />)
    expect(container.firstChild).toBeNull()
  })
})
