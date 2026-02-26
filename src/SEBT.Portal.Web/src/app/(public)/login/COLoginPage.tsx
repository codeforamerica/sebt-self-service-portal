import { TextLink } from '@/components/ui'
import { getStateLinks } from '@/lib/links'
import { getTranslations } from '@/lib/translations'
import Link from 'next/link'

import type { StateCode } from '@/lib/state'

export function COLoginPage({ state }: { state: StateCode }) {
  const links = getStateLinks(state)
  const t = getTranslations('login')
  const tCommon = getTranslations('common')

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
            {/* TODO: Update href to MyCO external auth URL when available */}
            <Link
              href="#"
              className="usa-button bg-primary-dark text-white border-primary-dark"
            >
              {tCommon('logIn')}
            </Link>
          </div>

          <div className="margin-top-2">
            {/* TODO: Update href to MyCO external auth URL (Spanish) when available */}
            <Link
              href="#"
              className="usa-button usa-button--outline border-primary text-primary"
              lang="es"
            >
              {tCommon('logInEsp')}
            </Link>
          </div>

          <p className="margin-top-4 font-sans-sm">
            <TextLink href={links.external.contactUsAssistance}>
              {t('logInDisclaimerBody2')}
            </TextLink>
          </p>
        </section>
      </div>
    </div>
  )
}
