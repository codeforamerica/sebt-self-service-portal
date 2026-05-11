'use client'

import type { StateCode } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import { MyColoradoLogo } from './MyColoradoLogo'

export function COLoginPage({ state }: { state: StateCode }) {
  const { t } = useTranslation('login')
  const { t: tCommon } = useTranslation('common')

  function startOidcLogin(language: string) {
    // Persist the user's language choice so the UI matches after the redirect.
    localStorage.setItem('i18nextLng', language)
    // Navigate to the server-side authorize endpoint, which builds the full
    // authorization URL and returns a 302 redirect to PingOne. The browser
    // never sees the authorization endpoint URL (V04 fix).
    window.location.href = `/api/auth/oidc/${state}/authorize?language=${encodeURIComponent(language)}`
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="login-title">
          <h1
            id="login-title"
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3 text-primary-dark"
          >
            {t('title')}
          </h1>

          <p className="margin-top-4 font-sans-sm">{t('logInDisclaimerBody1')}</p>

          <div className="margin-top-4">
            <button
              type="button"
              onClick={() => startOidcLogin('en')}
              className="usa-button usa-button--mycolorado display-flex flex-align-center"
            >
              <MyColoradoLogo className="margin-right-1" />
              {tCommon('logIn')}
            </button>
          </div>

          <div className="margin-top-2">
            <button
              type="button"
              onClick={() => startOidcLogin('es')}
              className="usa-button usa-button--outline usa-button--mycolorado display-flex flex-align-center"
              lang="es"
            >
              <MyColoradoLogo className="margin-right-1" />
              {tCommon('logInEsp')}
            </button>
          </div>
        </section>
      </div>
    </div>
  )
}
