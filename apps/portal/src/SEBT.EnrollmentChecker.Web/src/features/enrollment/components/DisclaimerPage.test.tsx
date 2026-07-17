import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DisclaimerPage } from './DisclaimerPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

describe('DisclaimerPage', () => {
  it('renders heading, body and two buttons', () => {
    render(<DisclaimerPage />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
  })

  it('navigates to / on Back', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('navigates to /check on Continue', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })
})
