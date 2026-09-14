import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApplicationAvailable } from './ApplicationAvailable'

describe('ApplicationAvailable', () => {
  it('links to the configured application destination', () => {
    render(<ApplicationAvailable applyHref="https://apply.example.gov/" />)
    expect(screen.getByTestId('apply-online-link')).toHaveAttribute(
      'href',
      'https://apply.example.gov/'
    )
  })

  it('drops the online link when no destination is configured', () => {
    render(<ApplicationAvailable applyHref={null} />)
    expect(screen.queryByTestId('apply-online-link')).toBeNull()
  })

  // Libraries stock forms regardless of an online destination, so this note
  // must survive on its own. i18n initialises with one state's resources (see
  // vitest.config.ts), so this state's copy echoes its key back — that the key
  // renders at all is what matters here.
  it('keeps the paper-application note when the online link degrades away', () => {
    const { container } = render(<ApplicationAvailable applyHref={null} />)
    expect(container).toHaveTextContent(/libraries|applyForSebtLibraryApplications/i)
  })
})
