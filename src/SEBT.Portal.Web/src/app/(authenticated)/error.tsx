'use client'

import { Alert, Button } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import type { ErrorProps } from '../types'

export default function AuthenticatedError({ error, reset }: ErrorProps) {
  const { t } = useTranslation('common')

  useEffect(() => {
    // TODO: Log error to monitoring service in production
    console.error('Authenticated page error:', error)
  }, [error])

  // Check if it's an authentication error
  const isAuthError =
    error.message?.toLowerCase().includes('unauthorized') ||
    error.message?.toLowerCase().includes('session') ||
    error.message?.toLowerCase().includes('authentication')

  return (
    <section
      className="usa-section"
      aria-labelledby="error-heading"
    >
      <div className="grid-container">
        <Alert
          variant="error"
          heading={isAuthError ? t('errorSessionExpired') : t('errorSomethingWentWrong')}
        >
          <p>{isAuthError ? t('errorSessionExpiredBody') : t('errorPageBody')}</p>
          {error.digest && (
            <p className="font-mono text-base-dark margin-top-1">
              {t('errorId')}
              {error.digest}
            </p>
          )}
          <div className="margin-top-2">
            {isAuthError ? (
              <Button
                type="button"
                onClick={() => (window.location.href = '/login')}
              >
                {t('errorLogInAgain')}
              </Button>
            ) : (
              <>
                <Button
                  type="button"
                  onClick={reset}
                  className="margin-right-2"
                >
                  {t('errorTryAgain')}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => (window.location.href = '/login')}
                >
                  {t('errorLogInAgain')}
                </Button>
              </>
            )}
          </div>
        </Alert>
      </div>
    </section>
  )
}
