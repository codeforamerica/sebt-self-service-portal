'use client'

import { useTranslation } from 'react-i18next'

import { Alert, Button } from '@sebt/design-system'

interface DocVerifyResubmitProps {
  onResubmit: () => void
  isResubmitting: boolean
  error?: string | null
}

// TODO: Use t('resubmit*') keys once they are available in dc.csv. The retry copy is a content
// gap until Laura's reason-code mapping doc lands; ship with generic copy first and surface
// reason-specific branches (blur, glare, unsupported ID) when the mapping is in.
// TODO: Sandbox observation 2026-04-28 — Socure coerces a consent decline into the same
// RESUBMIT verdict as a poor-quality capture, so this screen also greets users who actively
// declined the DocV terms. The "we weren't able to verify your identity from the document
// you uploaded" line is misleading in that case (they didn't upload anything). Confirm prod
// behavior with Laura, then split copy by reason if consent-decline lands here in prod too.
export function DocVerifyResubmit({ onResubmit, isResubmitting, error }: DocVerifyResubmitProps) {
  const { t } = useTranslation('idProofing')

  return (
    <section aria-labelledby="doc-verify-resubmit-title">
      {error && (
        <Alert
          variant="error"
          slim
          className="margin-bottom-2"
        >
          {error}
        </Alert>
      )}

      <h1
        id="doc-verify-resubmit-title"
        className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
      >
        {t('resubmitHeading', "Let's try that again")}
      </h1>

      <p className="font-sans-sm">
        {t(
          'resubmitBody',
          "We weren't able to verify your identity from the document you uploaded. " +
            'Make sure the image is clear, well-lit, and shows the full ID, then try again.'
        )}
      </p>

      <div className="margin-top-3">
        <Button
          type="button"
          onClick={onResubmit}
          isLoading={isResubmitting}
          loadingText={t('resubmitLoading', 'Starting retry...')}
          disabled={isResubmitting}
        >
          {t('resubmitActionTryAgain', 'Try again')}
        </Button>
      </div>
    </section>
  )
}
