import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { EnrolledSection } from './EnrolledSection'

const enrolled: ChildCheckApiResponse[] = [
  { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
]

describe('EnrolledSection', () => {
  it('renders enrolled children', () => {
    render(<EnrolledSection children={enrolled} />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('renders nothing when empty', () => {
    const { container } = render(<EnrolledSection children={[]} />)
    expect(container.firstChild).toBeNull()
  })
})
