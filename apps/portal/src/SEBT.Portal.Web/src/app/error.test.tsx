import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import Error from './error'

vi.mock('next/navigation', () => ({
  usePathname: () => '/login'
}))

// errorSomethingWentWrong has no row in the content sheet yet; the page ships its fallback copy.
const FALLBACK_HEADING = 'Something went wrong'

describe('Error boundary', () => {
  it('labels the section with a real level-1 heading', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})

    render(
      <Error
        error={new globalThis.Error('boom')}
        reset={() => {}}
      />
    )

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveAttribute('id', 'error-heading')
    expect(heading).toHaveTextContent(FALLBACK_HEADING)
    expect(screen.getByRole('region', { name: FALLBACK_HEADING })).toBeInTheDocument()
  })
})
