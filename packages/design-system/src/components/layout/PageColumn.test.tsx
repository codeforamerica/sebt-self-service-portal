import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { PageColumn } from './PageColumn'

describe('PageColumn', () => {
  it('renders children in a 480px column centered inside the 1024px page frame', () => {
    render(
      <PageColumn>
        <p>page content</p>
      </PageColumn>
    )

    const column = screen.getByText('page content').parentElement
    expect(column).toHaveClass('maxw-mobile-lg', 'margin-x-auto')

    const frame = column?.parentElement
    expect(frame).toHaveClass('grid-container')
    expect(frame).not.toHaveClass('maxw-tablet')
    expect(frame?.parentElement).toHaveClass('usa-section')
  })
})
