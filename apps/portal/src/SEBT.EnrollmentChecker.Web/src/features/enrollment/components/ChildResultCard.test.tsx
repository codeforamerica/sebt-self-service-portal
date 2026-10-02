import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ChildResultCard } from './ChildResultCard'

describe('ChildResultCard', () => {
  it('shows enrolled status', () => {
    render(<ChildResultCard firstName="Jane" lastName="Doe" displayStatus="enrolled" />)
    expect(screen.getByText(/Jane Doe/i)).toBeInTheDocument()
  })

  it('shows notEnrolled status', () => {
    render(<ChildResultCard firstName="John" lastName="Smith" displayStatus="notEnrolled" />)
    expect(screen.getByText(/John Smith/i)).toBeInTheDocument()
  })

  it('renders an all-caps name in sentence casing', () => {
    render(
      <ChildResultCard
        firstName="JANE"
        lastName="DOE"
        displayStatus="enrolled"
      />
    )
    expect(screen.getByText('Jane Doe')).toBeInTheDocument()
  })

  it('leaves a deliberately mixed-case name untouched', () => {
    render(
      <ChildResultCard
        firstName="Jane"
        lastName="MacDonald"
        displayStatus="enrolled"
      />
    )
    expect(screen.getByText('Jane MacDonald')).toBeInTheDocument()
  })

  it('shows error status with message', () => {
    render(<ChildResultCard firstName="A" lastName="B" displayStatus="error" errorMessage="Service unavailable" />)
    expect(screen.getByText(/Service unavailable/i)).toBeInTheDocument()
  })

  it('shows no error message for enrolled status', () => {
    render(<ChildResultCard firstName="Jane" lastName="Doe" displayStatus="enrolled" />)
    expect(screen.queryByText(/Service unavailable/i)).not.toBeInTheDocument()
  })

  it('shows no error message when errorMessage is null', () => {
    render(<ChildResultCard firstName="Jane" lastName="Doe" displayStatus="error" errorMessage={null} />)
    // error paragraph should not exist when errorMessage is null
    expect(screen.queryByRole('paragraph', { name: /error/i })).not.toBeInTheDocument()
    expect(document.querySelector('p.text-error')).not.toBeInTheDocument()
  })
})
