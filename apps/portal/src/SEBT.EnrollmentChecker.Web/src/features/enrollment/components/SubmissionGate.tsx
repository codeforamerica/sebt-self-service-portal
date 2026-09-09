'use client'

import { Alert, LoadingInterstitial } from '@sebt/design-system'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { getRateLimitErrorMessage } from '../copy/submitErrorCopy'
import type { EnrollmentSubmit } from '../hooks/useEnrollmentSubmit'
import { ErrorResultPage } from './ErrorResultPage'

interface SubmissionGateProps {
  submission: EnrollmentSubmit
  portalUrl: string
  children: ReactNode
}

/**
 * Wraps whichever screen starts an enrollment check with that check's states.
 *
 * The interstitial IS this flow's processing treatment: the screen underneath is
 * replaced entirely while the check runs, so it carries no busy props. A
 * whole-check failure replaces it with the full error page, while rate limiting
 * keeps an inline alert — the fix there is simply waiting and resubmitting the
 * form that is already on screen.
 */
export function SubmissionGate({ submission, portalUrl, children }: SubmissionGateProps) {
  const { i18n } = useTranslation('dev')
  const { t: tProcessing } = useTranslation('step-upProcessing')

  if (submission.isSubmitting) {
    return (
      <div className="grid-container maxw-tablet">
        <LoadingInterstitial
          title={tProcessing('title', 'Please wait...')}
          message={tProcessing(
            'body',
            'Do not exit the page. Checking to see if we have enough information.'
          )}
        />
      </div>
    )
  }

  if (submission.errorKind === 'generic') {
    return <ErrorResultPage portalUrl={portalUrl} />
  }

  return (
    <>
      {submission.errorKind === 'rateLimit' && (
        <Alert variant="error">{getRateLimitErrorMessage(i18n.language)}</Alert>
      )}
      {children}
    </>
  )
}
