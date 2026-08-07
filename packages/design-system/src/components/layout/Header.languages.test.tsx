/**
 * Header Language Wiring Tests
 *
 * Guards that the `state` prop Header receives is what decides the languages it
 * renders, rather than a deployment-wide value resolved once at module import.
 *
 * These tests deliberately render the real LanguageSelector and the real
 * `lib/i18n` module. `Header.test.tsx` mocks LanguageSelector away to stay
 * focused on the logo, and `vi.mock` is hoisted and file-scoped, so the two
 * concerns cannot share a file.
 */
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// Mock react-i18next: t(key) resolves to display text so the accessible names
// the queries rely on are present.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const translations: Record<string, string> = {
        languageSelector: 'Language selector',
        translate: 'Translate',
        english: 'English',
        español: 'Español',
        amharic: 'አማርኛ',
        bannerImageAltText: 'State logo'
      }
      // eslint-disable-next-line security/detect-object-injection -- key is typed, not user input
      return translations[key] ?? key
    },
    i18n: { language: 'en' }
  })
}))

vi.mock('next/image', () => ({
  default: ({ alt, ...props }: { alt: string; [key: string]: unknown }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      alt={alt}
      {...props}
    />
  )
}))

vi.mock('next/link', () => ({
  default: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
    <a {...props}>{children}</a>
  )
}))

/**
 * Import Header after stubbing the build-time state, so the module-level
 * `supportedLanguages` const in `lib/i18n` is evaluated against the stub.
 * Stubbing after import would not help — the binding is already resolved.
 */
async function renderHeaderForState(buildState: string, propState: 'dc' | 'co') {
  vi.stubEnv('NEXT_PUBLIC_STATE', buildState)
  vi.resetModules()
  const { Header } = await import('./Header')
  render(<Header state={propState} />)
}

describe('Header language wiring', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('renders the languages for the state prop, not the deployment default', async () => {
    // The deployment was built for DC (three languages) but this Header is asked
    // to render Colorado. Colorado wins.
    await renderHeaderForState('dc', 'co')

    const nav = screen.getByRole('navigation', { name: 'Language selector' })
    const buttons = nav.querySelectorAll('button')

    expect(buttons).toHaveLength(2)
    expect(buttons[0]).toHaveAttribute('lang', 'en')
    expect(buttons[1]).toHaveAttribute('lang', 'es')
    expect(nav.querySelector('[lang="am"]')).toBeNull()
  })

  it('renders two mobile menu items for CO within a DC build', async () => {
    const user = userEvent.setup()
    await renderHeaderForState('dc', 'co')

    await user.click(screen.getByRole('button', { name: /translate/i }))

    const items = screen.getAllByRole('menuitem')

    expect(items).toHaveLength(2)
    items.forEach((item) => {
      expect(item).not.toHaveAttribute('lang', 'am')
      expect(item.textContent?.trim()).not.toBe('')
    })
  })

  it('renders all three languages for DC', async () => {
    await renderHeaderForState('dc', 'dc')

    const nav = screen.getByRole('navigation', { name: 'Language selector' })

    expect(nav.querySelectorAll('button')).toHaveLength(3)
    expect(nav.querySelector('[lang="am"]')).toBeInTheDocument()
  })

  it('renders CO languages even when the build state is Colorado', async () => {
    // The agreeing case. Fixing the prop path must not break the path that was
    // already correct in a real CO deployment.
    await renderHeaderForState('co', 'co')

    const nav = screen.getByRole('navigation', { name: 'Language selector' })

    expect(nav.querySelectorAll('button')).toHaveLength(2)
    expect(nav.querySelector('[lang="am"]')).toBeNull()
  })
})
