'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@sebt/design-system'

// SessionStorage keys — set by IdProofingForm or DocVerifyPage before navigating here
const SK_REASON = 'offboarding_reason'
const SK_CAN_APPLY = 'offboarding_canApply'

interface OffBoardingPageProps {
  contactLink: string
  /** URL for the "Apply now" action, shown when canApply is true */
  applyLink?: string
}

/**
 * Minimal off-boarding page for users who fail ID proofing or document verification.
 *
 * Reads offboardingReason and canApply from sessionStorage (set by the referring page).
 */
export function OffBoardingPage({ contactLink, applyLink }: OffBoardingPageProps) {
  const router = useRouter()
  const { t } = useTranslation('offBoarding')
  const { t: tCommon } = useTranslation('common')

  // Read sessionStorage once on initial render (lazy initializer avoids setState-in-effect)
  const [reason] = useState(() => {
    if (typeof window === 'undefined') return null
    return sessionStorage.getItem(SK_REASON)
  })
  const [canApply] = useState(() => {
    if (typeof window === 'undefined') return false
    return sessionStorage.getItem(SK_CAN_APPLY) === 'true'
  })

  // Single reason branch so far: "noIdProvided" needs a tell-the-user-what-to-do
  // tone rather than the generic failure copy. Other reasons (null, unknown,
  // idProofingFailed, docVerificationFailed, ...) keep the existing generic path.
  const isNoIdProvided = reason === 'noIdProvided'

  // TODO: Use t('offboarding.noIdProvided.heading') once key is available in dc.csv
  const noIdHeading = 'We need an ID to verify you'
  // TODO: Use t('offboarding.noIdProvided.body') once key is available in dc.csv
  const noIdBody =
    "To confirm your identity, we need one of the listed IDs. If you don't have any of these IDs, contact us for help."

  // Clean up sessionStorage on unmount — the values have been read
  useEffect(() => {
    return () => {
      sessionStorage.removeItem(SK_REASON)
      sessionStorage.removeItem(SK_CAN_APPLY)
    }
  }, [])

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="offboarding-title">
          <h1
            id="offboarding-title"
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
          >
            {isNoIdProvided ? noIdHeading : t('coLoadedTitle')}
          </h1>

          <p className="font-sans-sm">{isNoIdProvided ? noIdBody : t('coLoadedBody1')}</p>

          <div className="margin-top-3">
            <Button
              type="button"
              className="usa-button--outline margin-right-2"
              onClick={() => {
                router.push('/login/id-proofing')
              }}
            >
              {tCommon('back')}
            </Button>

            <a
              href={contactLink}
              target="_blank"
              rel="noopener noreferrer"
              className="usa-button usa-button--outline"
            >
              {tCommon('linkContactUs')}
            </a>
          </div>

          {/*
            Conditional "Apply now" section. Shown when canApply is true,
            but suppressed for the noIdProvided reason because the user hasn't
            actually failed verification; they just didn't provide an ID yet.
          */}
          {canApply && !isNoIdProvided && (
            <div className="margin-top-4">
              <p className="font-sans-sm">{t('coLoadedBody2')}</p>

              {applyLink && (
                <a
                  href={applyLink}
                  className="usa-button margin-top-2"
                >
                  {t('coLoadedAction2')}
                </a>
              )}
            </div>
          )}

          {/* FAQs placeholder */}
          <div className="margin-top-6">
            <h2 className="font-sans-lg text-bold">{tCommon('linkFaqs')}</h2>
          </div>

          {/* Contact Us */}
          <div className="margin-top-4">
            <h2 className="font-sans-lg text-bold">{tCommon('linkFaqs')}</h2>
            <p className="font-sans-sm">
              <a
                href={contactLink}
                target="_blank"
                rel="noopener noreferrer"
                className="usa-link"
              >
                {/* TODO check on this copy */}
                {t('offboardingContactUsLink', 'Need help? Contact us.')}
              </a>
            </p>
          </div>
        </section>
      </div>
    </div>
  )
}
