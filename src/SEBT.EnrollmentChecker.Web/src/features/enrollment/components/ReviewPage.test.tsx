import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { EnrollmentProvider, useEnrollment } from '../context/EnrollmentContext'
import { ReviewPage } from './ReviewPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

// Helper to pre-populate context with a child
function ReviewPageWithChild({ onSubmit }: { onSubmit: () => void }) {
  return (
    <EnrollmentProvider>
      <Seeder />
      <ReviewPage onSubmit={onSubmit} />
    </EnrollmentProvider>
  )
}
function Seeder() {
  const { addChild } = useEnrollment()
  act(() => {
    addChild({ firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
  })
  return null
}

describe('ReviewPage', () => {
  it('lists added children', async () => {
    render(<ReviewPageWithChild onSubmit={vi.fn()} />)
    expect(await screen.findByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('calls onSubmit when Submit is clicked', async () => {
    const onSubmit = vi.fn()
    render(<ReviewPageWithChild onSubmit={onSubmit} />)
    await screen.findByText(/Jane Doe/i)
    await userEvent.click(screen.getByRole('button', { name: /submit/i }))
    expect(onSubmit).toHaveBeenCalled()
  })

  it('navigates to /check when Add Another is clicked', async () => {
    render(<ReviewPageWithChild onSubmit={vi.fn()} />)
    await screen.findByText(/Jane Doe/i)
    await userEvent.click(screen.getByRole('button', { name: /add another child/i }))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })
})
