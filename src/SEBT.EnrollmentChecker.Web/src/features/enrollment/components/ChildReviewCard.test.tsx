import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChildReviewCard } from './ChildReviewCard'

const child = { id: '1', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' }

describe('ChildReviewCard', () => {
  it('displays the child name and DOB', () => {
    render(<ChildReviewCard child={child} onEdit={vi.fn()} onRemove={vi.fn()} />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
    expect(screen.getByText(/2015-04-12/)).toBeInTheDocument()
  })

  it('calls onEdit with child id', async () => {
    const onEdit = vi.fn()
    render(<ChildReviewCard child={child} onEdit={onEdit} onRemove={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: /edit/i }))
    expect(onEdit).toHaveBeenCalledWith('1')
  })

  it('calls onRemove with child id', async () => {
    const onRemove = vi.fn()
    render(<ChildReviewCard child={child} onEdit={vi.fn()} onRemove={onRemove} />)
    await userEvent.click(screen.getByRole('button', { name: /remove/i }))
    expect(onRemove).toHaveBeenCalledWith('1')
  })
})
