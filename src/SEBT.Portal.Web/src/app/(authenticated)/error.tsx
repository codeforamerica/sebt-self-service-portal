'use client'

import { Alert, Button } from '@/components/ui'
import { useEffect } from 'react'

import type { ErrorProps } from '../types'

export default function AuthenticatedError({ error, reset }: ErrorProps) {
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
          heading={isAuthError ? 'Session expired' : 'Something went wrong'}
        >
          <p>
            {isAuthError
              ? 'Your session has expired. Please log in again to continue.'
              : 'We encountered an error loading this page. Please try again or log in again if the problem persists.'}
          </p>
          {error.digest && (
            <p className="font-mono text-base-dark margin-top-1">Error ID: {error.digest}</p>
          )}
          <div className="margin-top-2">
            {isAuthError ? (
              <Button
                type="button"
                onClick={() => (window.location.href = '/login')}
              >
                Log in again
              </Button>
            ) : (
              <>
                <Button
                  type="button"
                  onClick={reset}
                  className="margin-right-2"
                >
                  Try again
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => (window.location.href = '/login')}
                >
                  Log in again
                </Button>
              </>
            )}
          </div>
        </Alert>
      </div>
    </section>
  )
}
