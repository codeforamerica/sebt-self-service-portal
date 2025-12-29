import Link from 'next/link'

import { HelpSection } from '@/components/layout'
import { LoginForm } from '@/features/auth'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'

export default function LoginPage() {
  const state = getState()
  const links = getStateLinks(state)
  const t = getTranslations('login')
  const tDisclaimer = getTranslations('disclaimer')

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
              <Link
                href={links.external.contactUsAssistance}
                className="text-bold text-ink text-underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                {tDisclaimer('logInDisclaimerBody2')}
              </Link>
            </p>
          </section>
        </div>
      </div>

      <HelpSection state={state} />
    </>
  )
}
