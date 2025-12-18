/**
 * Home Page Unit Test (Co-located)
 *
 * Example of co-located test pattern - test file lives next to component
 * Benefits:
 * - Easier to find tests when working on components
 * - Clear ownership and visibility
 * - Better scalability for large codebases
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Home from './page'

describe('Home Page', () => {
  it('should render the page heading', () => {
    render(<Home />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('should display USWDS implementation heading', () => {
    render(<Home />)
    expect(screen.getByText(/USWDS Implementation with Figma Design Tokens/i)).toBeInTheDocument()
  })

  it('should show build workflow steps', () => {
    render(<Home />)
    expect(screen.getByText(/Build Workflow/i)).toBeInTheDocument()
    expect(screen.getByText(/Figma Tokens Studio/i)).toBeInTheDocument()
    expect(screen.getByText(/Token Transformation/i)).toBeInTheDocument()
  })

  it('should display USWDS-themed buttons', () => {
    render(<Home />)
    const primaryButton = screen.getByRole('button', { name: /Primary Button/i })
    expect(primaryButton).toHaveClass('usa-button')
  })

  it('should show state-specific theme information', () => {
    render(<Home />)
    // Should show either DC or CO theme active
    expect(screen.getByText(/DC Theme Active|CO Theme Active/i)).toBeInTheDocument()
  })
})
