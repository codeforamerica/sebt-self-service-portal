import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { DocVerifyResubmit } from './DocVerifyResubmit'

describe('DocVerifyResubmit', () => {
  it('renders a retry prompt heading and Try again CTA', () => {
    render(
      <DocVerifyResubmit
        onResubmit={vi.fn()}
        isResubmitting={false}
      />
    )

    expect(screen.getByRole('heading')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /try again/i })).toBeEnabled()
  })

  it('calls onResubmit when the user clicks Try again', async () => {
    const user = userEvent.setup()
    const onResubmit = vi.fn()

    render(
      <DocVerifyResubmit
        onResubmit={onResubmit}
        isResubmitting={false}
      />
    )

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(onResubmit).toHaveBeenCalledTimes(1)
  })

  it('disables the Try again button while resubmitting', () => {
    render(
      <DocVerifyResubmit
        onResubmit={vi.fn()}
        isResubmitting
      />
    )

    // When loading, the button label changes to the loadingText prop.
    const button = screen.getByRole('button', { name: /starting retry/i })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
  })

  it('shows an error alert when error prop is set', () => {
    render(
      <DocVerifyResubmit
        onResubmit={vi.fn()}
        isResubmitting={false}
        error="Could not start a retry. Please try again."
      />
    )

    expect(screen.getByRole('alert')).toHaveTextContent(/could not start a retry/i)
  })
})
