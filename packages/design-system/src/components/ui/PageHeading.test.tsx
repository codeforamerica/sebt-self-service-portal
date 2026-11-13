import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { StrictMode, useState, type ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getSiteDisplayName, getState } from '../../lib/state'
import { PageFocusProvider } from './PageFocusProvider'
import { PageHeading } from './PageHeading'

const mockPathname = vi.fn(() => '/one')

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname()
}))

const siteName = getSiteDisplayName(getState())

function withProvider(children: ReactNode) {
  return (
    <StrictMode>
      <PageFocusProvider>{children}</PageFocusProvider>
    </StrictMode>
  )
}

/** Mounts a sequence of screens inside one provider; `next()` advances to the following screen. */
function Screens({ screens }: { screens: ReactNode[] }) {
  const [index, setIndex] = useState(0)
  return (
    <>
      {screens[index]}
      <button
        type="button"
        onClick={() => setIndex((i) => i + 1)}
      >
        next
      </button>
    </>
  )
}

/** Mimics React swapping the hoisted title element when streamed route metadata lands. */
function replaceTitleElement(text: string) {
  document.querySelector('title')?.remove()
  const title = document.createElement('title')
  title.textContent = text
  document.head.appendChild(title)
}

describe('PageHeading', () => {
  beforeEach(() => {
    mockPathname.mockReturnValue('/one')
    document.title = 'Root title'
  })

  afterEach(() => {
    document.title = ''
  })

  it('renders a level-1 heading that can take programmatic focus', () => {
    render(withProvider(<PageHeading id="page-title">First page</PageHeading>))

    const heading = screen.getByRole('heading', {
      level: 1,
      name: 'First page'
    })
    expect(heading).toHaveAttribute('id', 'page-title')
    expect(heading).toHaveAttribute('tabindex', '-1')
  })

  it('does not take focus on the first screen after document load', () => {
    render(withProvider(<PageHeading id="page-title">First page</PageHeading>))

    expect(screen.getByRole('heading', { level: 1 })).not.toHaveFocus()
    expect(document.body).toHaveFocus()
  })

  it('takes focus when a different screen replaces the first', () => {
    render(
      withProvider(
        <Screens
          screens={[
            <PageHeading
              id="first"
              key="first"
            >
              First page
            </PageHeading>,
            <PageHeading
              id="second"
              key="second"
            >
              Second page
            </PageHeading>
          ]}
        />
      )
    )

    fireEvent.click(screen.getByRole('button', { name: 'next' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Second page' })).toHaveFocus()
  })

  it('takes focus when the same heading element changes screen in place', () => {
    function InPlaceSwap() {
      const [checking, setChecking] = useState(true)
      return (
        <>
          <PageHeading id={checking ? 'checking-title' : 'challenge-title'}>
            {checking ? 'Checking' : 'Challenge'}
          </PageHeading>
          <button
            type="button"
            onClick={() => setChecking(false)}
          >
            next
          </button>
        </>
      )
    }
    render(withProvider(<InPlaceSwap />))

    fireEvent.click(screen.getByRole('button', { name: 'next' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Challenge' })).toHaveFocus()
  })

  it('does not move focus when the same screen renders again', () => {
    render(
      withProvider(
        <Screens
          screens={[
            <PageHeading
              id="first"
              key="a"
            >
              First page
            </PageHeading>,
            <PageHeading
              id="first"
              key="b"
            >
              First page
            </PageHeading>
          ]}
        />
      )
    )
    const button = screen.getByRole('button', { name: 'next' })
    button.focus()

    fireEvent.click(button)

    expect(button).toHaveFocus()
  })

  it('treats a chain of loading screens as one screen and focuses the heading that follows', () => {
    render(
      withProvider(
        <Screens
          screens={[
            <PageHeading
              screenId="loading"
              key="loading-1"
            >
              Please wait
            </PageHeading>,
            <PageHeading
              screenId="loading"
              key="loading-2"
            >
              Please wait
            </PageHeading>,
            <PageHeading
              id="dashboard-title"
              key="dashboard"
            >
              Dashboard
            </PageHeading>
          ]}
        />
      )
    )
    const button = screen.getByRole('button', { name: 'next' })
    button.focus()

    fireEvent.click(button)
    expect(button).toHaveFocus()

    fireEvent.click(button)
    expect(screen.getByRole('heading', { level: 1, name: 'Dashboard' })).toHaveFocus()
  })

  it('sets the document title from its text and the site name', () => {
    render(withProvider(<PageHeading id="page-title">First page</PageHeading>))

    expect(document.title).toBe(`First page | ${siteName}`)
  })

  it('follows a text change without moving focus', () => {
    function Translated() {
      const [language, setLanguage] = useState<'en' | 'es'>('en')
      return (
        <>
          <PageHeading id="page-title">{language === 'en' ? 'Hello' : 'Hola'}</PageHeading>
          <button
            type="button"
            onClick={() => setLanguage('es')}
          >
            switch
          </button>
        </>
      )
    }
    render(withProvider(<Translated />))
    const button = screen.getByRole('button', { name: 'switch' })
    button.focus()

    fireEvent.click(button)

    expect(document.title).toBe(`Hola | ${siteName}`)
    expect(button).toHaveFocus()
  })

  it('restores the previous title when it unmounts', () => {
    const { unmount } = render(withProvider(<PageHeading id="page-title">First page</PageHeading>))
    expect(document.title).toBe(`First page | ${siteName}`)

    unmount()

    expect(document.title).toBe('Root title')
  })

  it('keeps its title when the framework replaces the title element after it mounted', async () => {
    render(withProvider(<PageHeading id="page-title">First page</PageHeading>))
    expect(document.title).toBe(`First page | ${siteName}`)

    replaceTitleElement('Streamed route title')

    await waitFor(() => expect(document.title).toBe(`First page | ${siteName}`))
  })

  it('restores the title the framework set, not a stale one, when it unmounts', async () => {
    const { unmount } = render(withProvider(<PageHeading id="page-title">First page</PageHeading>))
    replaceTitleElement('Streamed route title')
    await waitFor(() => expect(document.title).toBe(`First page | ${siteName}`))

    unmount()

    expect(document.title).toBe('Streamed route title')
  })

  it('gives the title back to the outer heading when a nested loading heading unmounts', () => {
    function Nested() {
      const [loading, setLoading] = useState(true)
      return (
        <>
          <PageHeading id="page-title">Verify your identity</PageHeading>
          {loading && <PageHeading screenId="loading">Please wait</PageHeading>}
          <button
            type="button"
            onClick={() => setLoading(false)}
          >
            done
          </button>
        </>
      )
    }
    render(withProvider(<Nested />))
    expect(document.title).toBe(`Please wait | ${siteName}`)

    fireEvent.click(screen.getByRole('button', { name: 'done' }))

    expect(document.title).toBe(`Verify your identity | ${siteName}`)
  })

  it('returns focus to the outer heading when a nested loading heading leaves, and focuses the next one', () => {
    function Nested() {
      const [loading, setLoading] = useState(false)
      return (
        <>
          <PageHeading id="page-title">Verify your identity</PageHeading>
          {loading && <PageHeading screenId="loading">Please wait</PageHeading>}
          <button
            type="button"
            onClick={() => setLoading((value) => !value)}
          >
            toggle
          </button>
        </>
      )
    }
    render(
      withProvider(
        <Screens
          screens={[
            <PageHeading id="login-title" key="login">Sign in</PageHeading>,
            <Nested key="nested" />
          ]}
        />
      )
    )
    fireEvent.click(screen.getByRole('button', { name: 'next' }))
    expect(screen.getByRole('heading', { name: 'Verify your identity' })).toHaveFocus()

    fireEvent.click(screen.getByRole('button', { name: 'toggle' }))
    expect(screen.getByRole('heading', { name: 'Please wait' })).toHaveFocus()

    fireEvent.click(screen.getByRole('button', { name: 'toggle' }))
    expect(screen.getByRole('heading', { name: 'Verify your identity' })).toHaveFocus()

    fireEvent.click(screen.getByRole('button', { name: 'toggle' }))
    expect(screen.getByRole('heading', { name: 'Please wait' })).toHaveFocus()
  })

  it('passes through other heading attributes', () => {
    render(
      withProvider(
        <PageHeading
          id="page-title"
          className="font-sans-xl"
          aria-describedby="hint"
        >
          First page
        </PageHeading>
      )
    )

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveClass('font-sans-xl')
    expect(heading).toHaveAttribute('aria-describedby', 'hint')
  })

  it('renders without a provider, leaving focus and title alone', () => {
    render(
      <Screens
        screens={[
          <PageHeading
            id="first"
            key="first"
          >
            First page
          </PageHeading>,
          <PageHeading
            id="second"
            key="second"
          >
            Second page
          </PageHeading>
        ]}
      />
    )
    const button = screen.getByRole('button', { name: 'next' })
    button.focus()

    fireEvent.click(button)

    expect(button).toHaveFocus()
    expect(document.title).toBe('Root title')
  })
})
