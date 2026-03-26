'use client'

import { Button } from '@sebt/design-system'

interface DocVerifyInterstitialProps {
  allowIdRetry: boolean
  isStartingChallenge: boolean
  onContinue: () => void
  onEnterIdNumber: () => void
  contactLink: string
}

export function DocVerifyInterstitial({
  allowIdRetry,
  isStartingChallenge,
  onContinue,
  onEnterIdNumber,
  contactLink
}: DocVerifyInterstitialProps) {
  return (
    <section aria-labelledby="doc-verify-title">
      {/* TODO: Use t('docVerify.title') once key is available in dc.csv */}
      <h1
        id="doc-verify-title"
        className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
      >
        We want to keep your account safe
      </h1>

      {/* TODO: Use t('docVerify.body') once key is available in dc.csv */}
      <p className="font-sans-sm">
        In order to confirm your identity we need to ask for additional documentation. On the next
        screen, we&apos;ll ask you to upload a photo of your:
      </p>

      <ul className="usa-list font-sans-sm">
        {/* TODO: Use t('docVerify.docTypes.*') once keys are available in dc.csv */}
        <li>driver&apos;s license</li>
        <li>foreign passport</li>
        <li>or another photo ID</li>
      </ul>

      {allowIdRetry && (
        <p className="font-sans-sm">
          {/* TODO: Use t('docVerify.skipHint') once key is available in dc.csv */}
          You can skip this step by going back and typing in your ID number instead.
        </p>
      )}

      <div className="margin-top-3">
        {allowIdRetry && (
          <Button
            type="button"
            className="usa-button--outline margin-right-2"
            onClick={onEnterIdNumber}
          >
            {/* TODO: Use t('docVerify.actionEnterId') once key is available in dc.csv */}
            Enter an ID number
          </Button>
        )}

        <Button
          type="button"
          onClick={onContinue}
          isLoading={isStartingChallenge}
          loadingText="Loading..."
          disabled={isStartingChallenge}
        >
          {/* TODO: Use t('docVerify.actionContinue') once key is available in dc.csv */}
          Continue
        </Button>
      </div>

      {/* FAQs placeholder */}
      <div className="margin-top-6">
        {/* TODO: Use t('docVerify.faqs') once key is available in dc.csv */}
        <h2 className="font-sans-lg text-bold">FAQs</h2>
      </div>

      {/* Contact Us */}
      <div className="margin-top-4">
        {/* TODO: Use t('docVerify.contactUs') once key is available in dc.csv */}
        <h2 className="font-sans-lg text-bold">Contact Us</h2>
        <p className="font-sans-sm">
          <a
            href={contactLink}
            target="_blank"
            rel="noopener noreferrer"
            className="usa-link"
          >
            {/* TODO: Use t('docVerify.contactUsLink') once key is available in dc.csv */}
            Need help? Contact us.
          </a>
        </p>
      </div>
    </section>
  )
}
