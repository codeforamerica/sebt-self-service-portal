'use client'

import { useTranslation } from 'react-i18next'

export function SignOutLink() {
  const { t } = useTranslation('dashboard')

  return (
    <a
      href="/api/auth/logout"
      className="usa-link font-sans-md text-bold line-height-sans-1"
    >
      {t('logout')}
    </a>
  )
}
