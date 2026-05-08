'use client'

import { Alert, Button } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import type { ErrorProps } from './types'

export default function Error({ error, reset }: ErrorProps) {
  const { t } = useTranslation('common')

  useEffect(() => {
    // TODO: Log error to monitoring service in production
    console.error('Application error:', error)
  }, [error])

  return (
    <section
      className="usa-section"
      aria-labelledby="error-heading"
    >
      <div className="grid-container">
        <Alert
          variant="error"
          heading={t('errorSomethingWentWrong')}
        >
          <p>{t('errorUnexpectedBody')}</p>
          {error.digest && (
            <p className="font-mono text-base-dark margin-top-1">
              {t('errorId')}
              {error.digest}
            </p>
          )}
          <Button
            type="button"
            onClick={reset}
            className="margin-top-2"
          >
            {t('errorTryAgain')}
          </Button>
        </Alert>
      </div>
    </section>
  )
}
