/**
 * MaintenancePageContent Unit Tests
 *
 * react-i18next is mocked so t(key) returns "namespace:key" — asserting the
 * prefix proves the component binds t() to the namespace it was given. Hrefs
 * are asserted against the links config (getStateLinks) rather than literals,
 * so the CO help-desk address never appears in test source.
 */
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { getStateLinks } from '../../lib/links'

import { MaintenancePageContent } from './MaintenancePageContent'

vi.mock('react-i18next', () => ({
  useTranslation: (ns: string) => ({
    t: (key: string) => `${ns}:${key}`,
    i18n: { language: 'en' }
  })
}))

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

describe('MaintenancePageContent (DC portal)', () => {
  function renderDc() {
    return render(
      <MaintenancePageContent
        namespace="maintenancePortal"
        state="dc"
      />
    )
  }

  it('renders a single h1 with the title from the given namespace', () => {
    renderDc()
    const headings = screen.getAllByRole('heading', { level: 1 })
    expect(headings).toHaveLength(1)
    expect(headings[0]).toHaveTextContent('maintenancePortal:title')
  })

  it('renders the body copy', () => {
    renderDc()
    expect(screen.getByText('maintenancePortal:body1')).toBeInTheDocument()
  })

  it('renders an outline state-site link and a filled contact link', () => {
    renderDc()
    const stateSite = screen.getByRole('link', { name: 'maintenancePortal:action1' })
    expect(stateSite).toHaveAttribute('href', getStateLinks('dc').help.sebtMainSite)
    expect(stateSite).toHaveClass('usa-button', 'usa-button--outline')

    const contact = screen.getByRole('link', { name: 'maintenancePortal:action2' })
    expect(contact).toHaveAttribute('href', getStateLinks('dc').help.contactUs)
    expect(contact).toHaveClass('usa-button')
    expect(contact).not.toHaveClass('usa-button--outline')
  })

  it('keeps the DC actions in a row', () => {
    renderDc()
    const row = screen.getByRole('link', { name: 'maintenancePortal:action1' }).parentElement
    expect(row?.className).not.toContain('flex-column')
  })

  it('neutralizes the USWDS default trailing margin so the row fits at mobile width', () => {
    // usa-button carries a default margin-right; combined with the gap between the
    // buttons it pushed the row ~5px past a 375px viewport, wrapping the contact
    // button onto a second line (the S.11 mockups show a single row on mobile).
    renderDc()
    const contact = screen.getByRole('link', { name: 'maintenancePortal:action2' })
    expect(contact).toHaveClass('margin-right-0')
  })
})

describe('MaintenancePageContent (CO checker)', () => {
  function renderCo() {
    return render(
      <MaintenancePageContent
        namespace="maintenanceEnrollmentChecker"
        state="co"
      />
    )
  }

  it('links the actions to the CO destinations, contact as mailto', () => {
    renderCo()
    expect(screen.getByRole('link', { name: 'maintenanceEnrollmentChecker:action1' })).toHaveAttribute(
      'href',
      getStateLinks('co').help.sebtMainSite
    )

    const contactHref = getStateLinks('co').help.contactUs
    expect(contactHref).toMatch(/^mailto:/)
    expect(screen.getByRole('link', { name: 'maintenanceEnrollmentChecker:action2' })).toHaveAttribute(
      'href',
      contactHref
    )
  })

  it('stacks the CO actions with a full-width outline button', () => {
    renderCo()
    const stateSite = screen.getByRole('link', { name: 'maintenanceEnrollmentChecker:action1' })
    expect(stateSite.parentElement?.className).toContain('flex-column')
    expect(stateSite).toHaveClass('width-full')
  })
})
