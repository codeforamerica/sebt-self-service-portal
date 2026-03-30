import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { SummaryBox } from './SummaryBox'

describe('SummaryBox', () => {
  it('renders children', () => {
    render(
      <SummaryBox>
        <p>Summary content</p>
      </SummaryBox>
    )
    expect(screen.getByText('Summary content')).toBeInTheDocument()
  })
})
