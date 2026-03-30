import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChildReviewCard } from './ChildReviewCard'

const child = { id: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' }

describe('ChildReviewCard', () => {
  it('displays the child name', () => {
    render(<ChildReviewCard child={child} onEdit={vi.fn()} />)
    expect(screen.getByText('Jane Doe')).toBeInTheDocument()
  })

  it('formats the birthdate using locale-aware formatting', () => {
    render(<ChildReviewCard child={child} onEdit={vi.fn()} />)
    // Intl.DateTimeFormat with 'en' locale produces "April 12, 2015"
    expect(screen.getByText('April 12, 2015')).toBeInTheDocument()
  })

  it('includes middle initial when middleName is present', () => {
    const childWithMiddle = { ...child, middleName: 'Marie' }
    render(<ChildReviewCard child={childWithMiddle} onEdit={vi.fn()} />)
    expect(screen.getByText('Jane M. Doe')).toBeInTheDocument()
  })

  it('calls onEdit with child id when update link is clicked', async () => {
    const onEdit = vi.fn()
    render(<ChildReviewCard child={child} onEdit={onEdit} />)
    await userEvent.click(screen.getByRole('button', { name: /update this child/i }))
    expect(onEdit).toHaveBeenCalledWith('1')
  })
})
