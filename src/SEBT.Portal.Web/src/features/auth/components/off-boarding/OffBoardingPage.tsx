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
  const { t } = useTranslation('idProofing')

  // Read sessionStorage once on initial render (lazy initializer avoids setState-in-effect)
  const [reason] = useState(() => {
    if (typeof window === 'undefined') return null
    return sessionStorage.getItem(SK_REASON)
  })
  const [canApply] = useState(() => {
    if (typeof window === 'undefined') return false
    return sessionStorage.getItem(SK_CAN_APPLY) === 'true'
  })

  // reason is available for future copy differentiation by offboarding scenario
  void reason

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
            {t(
              'offboardingHeading',
              "We're sorry, we aren't able to show your DC SUN Bucks information"
            )}
          </h1>

          <p className="font-sans-sm">
            {t(
              'offboardingBody',
              'You can go back to enter an ID number, or contact us if you need more help.'
            )}
          </p>

          <div className="margin-top-3">
            <Button
              type="button"
              className="usa-button--outline margin-right-2"
              onClick={() => {
                router.push('/login/id-proofing')
              }}
            >
              {t('offboardingActionBack', 'Back')}
            </Button>

            <a
              href={contactLink}
              target="_blank"
              rel="noopener noreferrer"
              className="usa-button usa-button--outline"
            >
              {t('offboardingActionContact', 'Contact us')}
            </a>
          </div>

          {/* Conditional "Apply now" section (D7) — shown when canApply is true */}
          {canApply && (
            <div className="margin-top-4">
              <p className="font-sans-sm">
                {t(
                  'offboardingApplyBody',
                  'If you\'re not sure what to do, or you want to apply for DC SUN Bucks, we can help you find out if you need to apply for your child. Tap "Apply now" and enter your child\'s information.'
                )}
              </p>

              {applyLink && (
                <a
                  href={applyLink}
                  className="usa-button margin-top-2"
                >
                  {t('offboardingActionApply', 'Apply now')}
                </a>
              )}
            </div>
          )}

          {/* FAQs placeholder */}
          <div className="margin-top-6">
            <h2 className="font-sans-lg text-bold">{t('offboardingFaqsHeading', 'FAQs')}</h2>
          </div>

          {/* Contact Us */}
          <div className="margin-top-4">
            <h2 className="font-sans-lg text-bold">
              {t('offboardingContactUsHeading', 'Contact Us')}
            </h2>
            <p className="font-sans-sm">
              <a
                href={contactLink}
                target="_blank"
                rel="noopener noreferrer"
                className="usa-link"
              >
                {t('offboardingContactUsLink', 'Need help? Contact us.')}
              </a>
            </p>
          </div>
        </section>
      </div>
    </div>
  )
}
