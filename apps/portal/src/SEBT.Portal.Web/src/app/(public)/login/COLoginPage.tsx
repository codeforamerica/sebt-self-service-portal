'use client'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import type { StateCode } from '@sebt/design-system'
import { TextLink, getStateLinks } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import { MyColoradoLogo } from './MyColoradoLogo'

function splitParagraphs(text: string): string[] {
  return text
    .split(/\r?\n\r?\n/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean)
}

function splitListItems(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((item) => item.trim())
    .filter(Boolean)
}

export function COLoginPage({ state }: { state: StateCode }) {
  const links = getStateLinks(state)
  const { t, i18n } = useTranslation('login')
  const { t: tCommon } = useTranslation('common')
  const { trackEvent } = useDataLayer()

  const aboutParagraphs = splitParagraphs(t('cardBody1'))
  const aboutListItems = splitListItems(t('cardBody2'))

  // The `logIn` translation key resolves to the current UI language's label, and
  // `logInEsp` resolves to the *other* language's label. Pair each button's link
  // target with its label so the user lands in the language they chose.
  const currentLang = i18n.language.startsWith('es') ? 'es' : 'en'
  const otherLang = currentLang === 'es' ? 'en' : 'es'

  function startOidcLogin(language: string) {
    trackEvent(AnalyticsEvents.OIDC_START)
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
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3 text-primary"
          >
            {t('title')}
          </h1>

          <p className="margin-top-4 font-sans-sm">{t('logInDisclaimerBody1')}</p>

          <div className="margin-top-4">
            <button
              type="button"
              onClick={() => startOidcLogin(currentLang)}
              className="usa-button usa-button--mycolorado display-flex flex-align-center"
              lang={currentLang}
              data-analytics-cta="login_cta"
            >
              <MyColoradoLogo className="margin-right-1" />
              {tCommon('logIn')}
            </button>
          </div>

          <div className="margin-top-2">
            <button
              type="button"
              onClick={() => startOidcLogin(otherLang)}
              className="usa-button usa-button--outline usa-button--mycolorado display-flex flex-align-center"
              lang={otherLang}
              data-analytics-cta="login_cta_alt_lang"
            >
              <MyColoradoLogo className="margin-right-1" />
              {tCommon('logInEsp')}
            </button>
          </div>

          <p className="margin-top-4 margin-bottom-1 font-sans-sm">{t('logInDisclaimerBody2')}</p>
          <p className="margin-top-0 font-sans-sm">
            <TextLink
              href={links.external.contactUsAssistance}
              target="_blank"
              rel="noopener noreferrer"
            >
              {t('logInDisclaimerBody3')}
            </TextLink>
          </p>
        </section>

        <section
          className="margin-top-4"
          aria-labelledby="about-portal-title"
        >
          <div className="usa-card__container">
            <div className="usa-card__body">
              <h2
                id="about-portal-title"
                className="usa-card__heading font-sans-lg text-bold margin-top-0"
              >
                {t('cardTitle')}
              </h2>

              {aboutParagraphs.map((paragraph) => (
                <p
                  key={paragraph}
                  className="font-sans-sm margin-bottom-2"
                >
                  {paragraph}
                </p>
              ))}

              <ul className="usa-list margin-top-0 margin-bottom-0">
                {aboutListItems.map((item) => (
                  <li
                    key={item}
                    className="font-sans-sm"
                  >
                    {item}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </section>
      </div>
    </div>
  )
}
