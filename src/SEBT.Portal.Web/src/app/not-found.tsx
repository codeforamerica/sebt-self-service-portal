'use client'

import { Alert } from '@sebt/design-system'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'

export default function NotFound() {
  const { t } = useTranslation('common')

  return (
    <section
      className="usa-section"
      aria-labelledby="not-found-heading"
    >
      <div className="grid-container">
        <Alert
          variant="error"
          heading={t('pageNotFound', 'Page not found')}
        >
          <p>
            {t(
              'pageNotFoundBody',
              'The page you are looking for does not exist or has been moved.'
            )}
          </p>
          <Link
            href="/"
            className="usa-button margin-top-2"
          >
            {t('returnToHome', 'Return to home')}
          </Link>
        </Alert>
      </div>
    </section>
  )
}
