import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { getStateLinks } from '@sebt/design-system'

import enDcMaintenance from '@/content/locales/en/dc/maintenancePortal.json'

import OutagePage from './page'

// Wiring test: renders the page against the portal's real i18next instance
// (initialized in test-setup from the generated locale resources), proving the
// maintenancePortal namespace flows through end to end. Component rendering
// details are covered in @sebt/design-system.

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

describe('OutagePage', () => {
  it('renders the maintenance copy from the generated locale bundle', () => {
    render(<OutagePage />)

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(enDcMaintenance.title)
    expect(screen.getByText(enDcMaintenance.body1)).toBeInTheDocument()
  })

  it('routes both actions to the DC destinations', () => {
    render(<OutagePage />)

    expect(screen.getByRole('link', { name: enDcMaintenance.action1 })).toHaveAttribute(
      'href',
      getStateLinks('dc').help.sebtMainSite
    )
    expect(screen.getByRole('link', { name: enDcMaintenance.action2 })).toHaveAttribute(
      'href',
      getStateLinks('dc').help.contactUs
    )
  })
})
