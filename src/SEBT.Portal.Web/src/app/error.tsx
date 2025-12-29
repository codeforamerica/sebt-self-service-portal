'use client'

import { Alert, Button } from '@/components/ui'
import { useEffect } from 'react'

import type { ErrorProps } from './types'

export default function Error({ error, reset }: ErrorProps) {
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
          heading="Something went wrong"
        >
          <p>
            An unexpected error occurred. Please try again or contact support if the problem
            persists.
          </p>
          {error.digest && (
            <p className="font-mono text-base-dark margin-top-1">Error ID: {error.digest}</p>
          )}
          <Button
            type="button"
            onClick={reset}
            className="margin-top-2"
          >
            Try again
          </Button>
        </Alert>
      </div>
    </section>
  )
}
