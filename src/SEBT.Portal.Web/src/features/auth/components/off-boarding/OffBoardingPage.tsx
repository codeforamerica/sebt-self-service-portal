'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

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
          {/* TODO: Use t('offboarding.title') once key is available in dc.csv */}
          <h1
            id="offboarding-title"
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
          >
            We&apos;re sorry, we aren&apos;t able to show your DC SUN Bucks information
          </h1>

          {/* TODO: Use t('offboarding.body') once key is available in dc.csv */}
          <p className="font-sans-sm">
            You can go back to enter an ID number, or contact us if you need more help.
          </p>

          <div className="margin-top-3">
            <Button
              type="button"
              className="usa-button--outline margin-right-2"
              onClick={() => {
                router.push('/login/id-proofing')
              }}
            >
              {/* TODO: Use t('offboarding.actionBack') once key is available in dc.csv */}
              Back
            </Button>

            <a
              href={contactLink}
              target="_blank"
              rel="noopener noreferrer"
              className="usa-button usa-button--outline"
            >
              {/* TODO: Use t('offboarding.actionContact') once key is available in dc.csv */}
              Contact us
            </a>
          </div>

          {/* Conditional "Apply now" section (D7) — shown when canApply is true */}
          {canApply && (
            <div className="margin-top-4">
              {/* TODO: Use t('offboarding.applyBody') once key is available in dc.csv */}
              <p className="font-sans-sm">
                If you&apos;re not sure what to do, or you want to apply for DC SUN Bucks, we can
                help you find out if you need to apply for your child. Tap &quot;Apply now&quot; and
                enter your child&apos;s information.
              </p>

              {applyLink && (
                <a
                  href={applyLink}
                  className="usa-button margin-top-2"
                >
                  {/* TODO: Use t('offboarding.actionApply') once key is available in dc.csv */}
                  Apply now
                </a>
              )}
            </div>
          )}

          {/* FAQs placeholder */}
          <div className="margin-top-6">
            {/* TODO: Use t('offboarding.faqs') once key is available in dc.csv */}
            <h2 className="font-sans-lg text-bold">FAQs</h2>
          </div>

          {/* Contact Us */}
          <div className="margin-top-4">
            {/* TODO: Use t('offboarding.contactUs') once key is available in dc.csv */}
            <h2 className="font-sans-lg text-bold">Contact Us</h2>
            <p className="font-sans-sm">
              <a
                href={contactLink}
                target="_blank"
                rel="noopener noreferrer"
                className="usa-link"
              >
                {/* TODO: Use t('offboarding.contactUsLink') once key is available in dc.csv */}
                Need help? Contact us.
              </a>
            </p>
          </div>
        </section>
      </div>
    </div>
  )
}
