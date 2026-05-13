'use client'

import { Alert, Button } from '@sebt/design-system'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import type { ErrorProps } from '../types'

export default function AuthenticatedError({ error, reset }: ErrorProps) {
  const { t } = useTranslation('common')
  const { t: tDev } = useTranslation('dev')
  const { t: tValidation } = useTranslation('validation')

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
          heading={
            isAuthError
              ? tDev('alertSession', 'Session expired')
              : // TODO update with correct string
                t('errorSomethingWentWrong', 'Something went wrong')
          }
        >
          <p>{isAuthError ? tDev('alertSessionClient') : tValidation('globalInternalError')}</p>
          {error.digest && (
            <p className="font-mono text-base-dark margin-top-1">
              {tDev('errorPrefix')}
              {error.digest}
            </p>
          )}
          <div className="margin-top-2">
            {isAuthError ? (
              <Button
                type="button"
                onClick={() => (window.location.href = '/login')}
              >
                {tDev('logInRefresh')}
              </Button>
            ) : (
              <>
                <Button
                  type="button"
                  onClick={reset}
                  className="margin-right-2"
                >
                  {tDev('alertTryAgain')}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => (window.location.href = '/login')}
                >
                  {tDev('logInRefresh')}
                </Button>
              </>
            )}
          </div>
        </Alert>
      </div>
    </section>
  )
}
