'use client'

import { LoginForm } from '@/features/auth'
import { TextLink, getState, getStateLinks, type StateCode } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import { COLoginPage } from './COLoginPage'

const loginPageOverrides: Partial<Record<StateCode, React.ComponentType<{ state: StateCode }>>> = {
  co: COLoginPage
}

export default function LoginPage() {
  const state = getState()
  const { t } = useTranslation('login')

  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  const Override = loginPageOverrides[state]
  if (Override) return <Override state={state} />

  const links = getStateLinks(state)

  return (
    <>
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section aria-labelledby="login-title">
            <h1
              id="login-title"
              className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
            >
              {t('title')}
            </h1>

            <p className="margin-top-4 font-sans-sm">{t('body')}</p>
            <LoginForm />

            <p className="margin-top-4 font-sans-sm">
              <TextLink
                href={links.external.contactUsAssistance}
                target="_blank"
                rel="noopener noreferrer"
              >
                {t('logInDisclaimerBody2')}
              </TextLink>
            </p>
          </section>
        </div>
      </div>
    </>
  )
}
