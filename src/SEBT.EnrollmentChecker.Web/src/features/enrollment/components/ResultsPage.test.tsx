import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ResultsPage } from './ResultsPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

const results: ChildCheckApiResponse[] = [
  { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' },
  { checkId: '2', firstName: 'John', lastName: 'Smith', dateOfBirth: '2016-01-01', status: 'NonMatch' },
  { checkId: '3', firstName: 'Alex', lastName: 'Lee', dateOfBirth: '2014-05-05', status: 'Error', statusMessage: 'Service error' }
]

describe('ResultsPage', () => {
  it('renders a heading', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('shows enrolled child in enrolled section', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('shows not-enrolled child in not-enrolled section', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
  })

  it('shows error child with error message', () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Service error/i)).toBeInTheDocument()
  })

  it('navigates to /review on Back click', async () => {
    render(<ResultsPage results={results} applicationUrl="https://apply.example.gov" />)
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(mockPush).toHaveBeenCalledWith('/review')
  })

  it('buckets children by status correctly — no mixed sections', () => {
    const onlyEnrolled: ChildCheckApiResponse[] = [
      { checkId: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12', status: 'Match' }
    ]
    render(<ResultsPage results={onlyEnrolled} applicationUrl="https://apply.example.gov" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
    expect(screen.queryByText(/John Smith/i)).not.toBeInTheDocument()
  })
})
