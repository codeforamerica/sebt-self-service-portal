import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { NotEnrolledSection } from './NotEnrolledSection'

const notEnrolled: ChildCheckApiResponse[] = [
  { checkId: '2', firstName: 'John', lastName: 'Smith', dateOfBirth: '2016-01-01', status: 'NonMatch' }
]

describe('NotEnrolledSection', () => {
  it('renders not-enrolled children and application link', () => {
    render(<NotEnrolledSection results={notEnrolled} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
    expect(screen.getByRole('link')).toHaveAttribute('href', 'https://apply.example.gov')
  })

  it('renders nothing when empty', () => {
    const { container } = render(<NotEnrolledSection results={[]} applicationUrl="" />)
    expect(container.firstChild).toBeNull()
  })
})
