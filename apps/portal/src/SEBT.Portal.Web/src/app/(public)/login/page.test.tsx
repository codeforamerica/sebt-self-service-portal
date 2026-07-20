/**
 * Login Page Unit Tests (Co-located)
 *
 * Tests the login page for both CO and DC states.
 * CO renders the external auth landing page (COLoginPage).
 * DC renders the OTP email form (LoginForm).
 */
import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import LoginPage from './page'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() })
}))

vi.mock('@sebt/design-system', () => ({
  getState: vi.fn(),
  getStateLinks: vi.fn().mockReturnValue({
    external: {
      contactUsAssistance: 'https://mycolorado.state.co.us/customer-support'
    }
  }),
  TextLink: ({
    href,
    children,
    target,
    rel
  }: {
    href: string
    children: React.ReactNode
    target?: string
    rel?: string
  }) => (
    <a
      href={href}
      target={target}
      rel={rel}
    >
      {children}
    </a>
  )
}))

// COLoginPage now uses the client-side useTranslation() hook (DC-187 fix).
// Mock the hook with state-specific copy so CO/DC tests stay isolated from
// the project's real i18n init (which boots in DC mode for tests).
//
// `logIn` resolves to the current UI language's label; `logInEsp` resolves to
// the other language. Mirrors the real locale files in `content/locales/{en,es}/co/common.json`.
const CO_TEST_TRANSLATIONS: Record<string, Record<string, Record<string, string>>> = {
  en: {
    login: {
      title: 'Access your Summer EBT account',
      body: 'Enter your email to receive a one-time code.',
      logInDisclaimerBody1:
        'After tapping "Log in" you\'ll be redirected to log in using your myColorado™ account.',
      logInDisclaimerBody2: 'Having trouble signing into the portal?',
      logInDisclaimerBody3: 'Contact myColorado® Support',
      cardTitle: 'About the Summer EBT portal',
      cardBody1:
        "The Summer EBT portal is the fastest way to manage your family's Summer EBT benefits without ever making a phone call.\n\nBy creating a secure account, the main parent or guardian on file can:",
      cardBody2:
        "Request a new Summer EBT card if their child's is lost, stolen, or damaged.\nUpdate your mailing address so you don't miss important mail.\nCheck benefits and card status for all enrolled children in your household.\nSee the application status for any applications that you submitted.\nOpt-in to email communications about your benefits."
    },
    common: {
      logIn: 'Log in with myColorado™',
      logInEsp: 'Iniciar sesión con myColorado™'
    }
  },
  es: {
    login: {
      title: 'Accede a tu cuenta de Summer EBT',
      body: 'Ingresa tu correo electrónico para recibir un código.',
      logInDisclaimerBody1:
        'Al tocar "Iniciar sesión" serás redirigido a iniciar sesión con tu cuenta myColorado™.',
      logInDisclaimerBody2: 'Contáctanos si necesitas ayuda para iniciar sesión.',
      logInDisclaimerBody3: 'Comunícate con la ayuda de myColorado®',
      cardTitle: 'Sobre el portal de Summer EBT',
      cardBody1:
        'El portal de Summer EBT es la forma más rápida de manejar los beneficios de Summer EBT de tu familia.\n\nAl crear una cuenta segura, el padre, madre o tutor principal registrado puede:',
      cardBody2: 'Pedir una nueva Tarjeta Summer EBT si la de tu niño/a se perdió.'
    },
    common: {
      logIn: 'Iniciar sesión con myColorado™',
      logInEsp: 'Log in with myColorado™'
    }
  }
}

const DC_TEST_TRANSLATIONS: Record<string, Record<string, Record<string, string>>> = {
  en: {
    login: {
      title: 'Access your Summer EBT account',
      body: 'Enter your email to receive a one-time code.',
      logInDisclaimerBody2: 'Contact us if you need assistance logging into your account.'
    },
    common: {}
  },
  es: {
    login: {
      title: 'Accede a tu cuenta de Summer EBT',
      body: 'Ingresa tu correo electrónico para recibir un código.',
      logInDisclaimerBody2: 'Contáctanos si necesitas ayuda para iniciar sesión.'
    },
    common: {}
  }
}

let mockLanguage: 'en' | 'es' = 'en'
let mockState: 'co' | 'dc' = 'co'

vi.mock('react-i18next', () => ({
  useTranslation: (namespace: string) => ({
    /* eslint-disable security/detect-object-injection -- test mock; namespace + key controlled */
    t: (key: string, defaultValue?: string) => {
      const translations = mockState === 'co' ? CO_TEST_TRANSLATIONS : DC_TEST_TRANSLATIONS
      return translations[mockLanguage]?.[namespace]?.[key] ?? defaultValue ?? key
    },
    /* eslint-enable security/detect-object-injection */
    i18n: { language: mockLanguage }
  })
}))

vi.mock('@/features/auth', () => ({
  LoginForm: () => <div data-testid="login-form">LoginForm</div>,
  useAuth: () => ({ isAuthenticated: false })
}))

// Spy on the data layer so we can assert OIDC_START fires from the CO buttons
// without booting a real DataLayer instance.
const mockTrackEvent = vi.fn()
vi.mock('@sebt/analytics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/analytics')>()
  return {
    ...actual,
    useDataLayer: () => ({
      trackEvent: mockTrackEvent,
      pageLoad: vi.fn(),
      setPageData: vi.fn(),
      setPageCategory: vi.fn(),
      setPageAttribute: vi.fn(),
      setUserData: vi.fn(),
      setUserProfile: vi.fn(),
      get: vi.fn()
    })
  }
})

import { getState } from '@sebt/design-system'
const mockGetState = vi.mocked(getState)

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockTrackEvent.mockClear()
    mockLanguage = 'en'
    mockState = 'co'
  })

  describe('CO state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('co')
      mockState = 'co'
    })

    it('renders the login title', () => {
      render(<LoginPage />)
      expect(
        screen.getByRole('heading', {
          name: /Access your Summer EBT account/i
        })
      ).toBeInTheDocument()
    })

    it('applies text-primary class to the title', () => {
      render(<LoginPage />)
      const heading = screen.getByRole('heading', {
        name: /Access your Summer EBT account/i
      })
      expect(heading).toHaveClass('text-primary')
    })

    it('renders the disclaimer body text', () => {
      render(<LoginPage />)
      expect(
        screen.getByText(/you'll be redirected to log in using your myColorado/i)
      ).toBeInTheDocument()
    })

    it('renders the myColorado support prompt and link', () => {
      render(<LoginPage />)
      expect(screen.getByText('Having trouble signing into the portal?')).toBeInTheDocument()

      const supportLink = screen.getByRole('link', { name: /Contact myColorado/i })
      expect(supportLink).toHaveAttribute('href', 'https://mycolorado.state.co.us/customer-support')
      expect(supportLink).toHaveAttribute('target', '_blank')
    })

    it('renders the about portal card with bullet list', () => {
      render(<LoginPage />)
      expect(
        screen.getByRole('heading', { name: /About the Summer EBT portal/i })
      ).toBeInTheDocument()
      expect(
        screen.getByText(/fastest way to manage your family's Summer EBT benefits/i)
      ).toBeInTheDocument()
      expect(
        screen.getByText(/Request a new Summer EBT card if their child's is lost/i)
      ).toBeInTheDocument()
      expect(
        screen.getByText(/Opt-in to email communications about your benefits/i)
      ).toBeInTheDocument()
    })

    it('renders the Log in button with myColorado branded styling', () => {
      render(<LoginPage />)
      const logInButton = screen.getByRole('button', { name: /Log in with myColorado/i })
      expect(logInButton).toHaveClass('usa-button')
      expect(logInButton).toHaveClass('usa-button--mycolorado')
      expect(logInButton).not.toHaveClass('usa-button--outline')
    })

    it('renders the Iniciar sesión button as an outlined myColorado variant', () => {
      render(<LoginPage />)
      const espButton = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })
      expect(espButton).toHaveAttribute('lang', 'es')
      expect(espButton).toHaveClass('usa-button--mycolorado')
      expect(espButton).toHaveClass('usa-button--outline')
    })

    it('renders the myColorado logo inside both auth buttons', () => {
      render(<LoginPage />)
      const logInButton = screen.getByRole('button', { name: /Log in with myColorado/i })
      const espButton = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })
      expect(logInButton.querySelector('[data-testid="mycolorado-logo"]')).toBeInTheDocument()
      expect(espButton.querySelector('[data-testid="mycolorado-logo"]')).toBeInTheDocument()
    })

    it('does not render LoginForm', () => {
      render(<LoginPage />)
      expect(screen.queryByTestId('login-form')).not.toBeInTheDocument()
    })

    describe('analytics', () => {
      it('tags both auth buttons with data-analytics-cta for cta_click tracking', () => {
        render(<LoginPage />)
        const primary = screen.getByRole('button', { name: /Log in with myColorado/i })
        const secondary = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })

        expect(primary).toHaveAttribute('data-analytics-cta', 'login_cta')
        expect(secondary).toHaveAttribute('data-analytics-cta', 'login_cta_alt_lang')
      })

      it('fires oidc_start when the primary auth button is clicked', () => {
        render(<LoginPage />)
        const primary = screen.getByRole('button', { name: /Log in with myColorado/i })

        primary.click()

        expect(mockTrackEvent).toHaveBeenCalledWith('oidc_start')
      })

      it('fires oidc_start when the secondary auth button is clicked', () => {
        render(<LoginPage />)
        const secondary = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })

        secondary.click()

        expect(mockTrackEvent).toHaveBeenCalledWith('oidc_start')
      })
    })

    describe('language routing', () => {
      // Stub window.location.href so we can read what each button navigated to.
      let assignedHref: string
      const originalLocation = window.location
      beforeEach(() => {
        assignedHref = ''
        Object.defineProperty(window, 'location', {
          configurable: true,
          value: {
            get href() {
              return assignedHref
            },
            set href(value: string) {
              assignedHref = value
            }
          }
        })
        localStorage.clear()
      })
      afterEach(() => {
        Object.defineProperty(window, 'location', {
          configurable: true,
          value: originalLocation
        })
      })

      it('routes the primary button to the current UI language in English mode', () => {
        mockLanguage = 'en'
        render(<LoginPage />)
        const primary = screen.getByRole('button', { name: /Log in with myColorado/i })

        primary.click()

        expect(assignedHref).toContain('language=en')
        expect(localStorage.getItem('i18nextLng')).toBe('en')
      })

      it('routes the secondary button to the other language in English mode', () => {
        mockLanguage = 'en'
        render(<LoginPage />)
        const secondary = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })

        secondary.click()

        expect(assignedHref).toContain('language=es')
        expect(localStorage.getItem('i18nextLng')).toBe('es')
      })

      it('routes the primary button to the current UI language in Spanish mode', () => {
        // Bug fix: in Spanish mode, the primary button label is Spanish; clicking it
        // should send the user to the Spanish-language MyCO flow, not the English one.
        mockLanguage = 'es'
        render(<LoginPage />)
        const primary = screen.getByRole('button', { name: /Iniciar sesión con myColorado/i })

        primary.click()

        expect(assignedHref).toContain('language=es')
        expect(localStorage.getItem('i18nextLng')).toBe('es')
      })

      it('routes the secondary button to the other language in Spanish mode', () => {
        mockLanguage = 'es'
        render(<LoginPage />)
        const secondary = screen.getByRole('button', { name: /Log in with myColorado/i })

        secondary.click()

        expect(assignedHref).toContain('language=en')
        expect(localStorage.getItem('i18nextLng')).toBe('en')
      })
    })
  })

  describe('DC state', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
      mockState = 'dc'
    })

    it('renders the contact assistance link', () => {
      render(<LoginPage />)
      expect(
        screen.getByText('Contact us if you need assistance logging into your account.')
      ).toBeInTheDocument()
    })

    it('renders LoginForm', () => {
      render(<LoginPage />)
      expect(screen.getByTestId('login-form')).toBeInTheDocument()
    })

    it('does not apply text-primary-dark to the title', () => {
      render(<LoginPage />)
      const heading = screen.getByRole('heading', { level: 1 })
      expect(heading).not.toHaveClass('text-primary-dark')
    })
  })
})
