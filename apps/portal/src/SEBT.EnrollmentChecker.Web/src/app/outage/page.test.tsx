import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { getStateLinks } from '@sebt/design-system/src/lib/links'

import enCoMaintenance from '@/content/locales/en/co/maintenanceEnrollmentChecker.json'

import OutagePage from './page'

// Wiring test: renders the page against the checker's real i18next instance
// (initialized in test-setup from the generated locale resources), proving the
// maintenanceEnrollmentChecker namespace flows through end to end. Component
// rendering details are covered in @sebt/design-system.

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

describe('OutagePage', () => {
  it('renders the maintenance copy from the generated locale bundle', () => {
    render(<OutagePage />)

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(enCoMaintenance.title)
    expect(screen.getByText(enCoMaintenance.body1)).toBeInTheDocument()
  })

  it('routes both actions to the state destinations', () => {
    render(<OutagePage />)

    expect(screen.getByRole('link', { name: enCoMaintenance.action1 })).toHaveAttribute(
      'href',
      getStateLinks('co').help.sebtMainSite
    )
    expect(screen.getByRole('link', { name: enCoMaintenance.action2 })).toHaveAttribute(
      'href',
      getStateLinks('co').help.contactUs
    )
  })
})
