'use client'

import { useTranslation } from 'react-i18next'

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
  const { t } = useTranslation('idProofing')

  return (
    <section aria-labelledby="doc-verify-title">
      <h1
        id="doc-verify-title"
        className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
      >
        {t('interstitialHeading', 'We want to keep your account safe')}
      </h1>

      <p className="font-sans-sm">
        {t(
          'interstitialBody',
          "In order to confirm your identity we need to ask for additional documentation. On the next screen, we'll ask you to upload a photo of your:"
        )}
      </p>

      <ul className="usa-list font-sans-sm">
        <li>{t('interstitialIdTypeDriversLicense', "driver's license")}</li>
        <li>{t('interstitialIdTypeForeignPassport', 'foreign passport')}</li>
        <li>{t('interstitialIdTypeOtherPhotoId', 'or another photo ID')}</li>
      </ul>

      {allowIdRetry && (
        <p className="font-sans-sm">
          {t(
            'interstitialSkipHint',
            'You can skip this step by going back and typing in your ID number instead.'
          )}
        </p>
      )}

      <div className="margin-top-3">
        {allowIdRetry && (
          <Button
            type="button"
            className="usa-button--outline margin-right-2"
            onClick={onEnterIdNumber}
          >
            {t('interstitialActionEnterId', 'Enter an ID number')}
          </Button>
        )}

        <Button
          type="button"
          onClick={onContinue}
          isLoading={isStartingChallenge}
          loadingText={t('interstitialLoading', 'Loading...')}
          disabled={isStartingChallenge}
        >
          {t('interstitialActionContinue', 'Continue')}
        </Button>
      </div>

      {/* FAQs placeholder */}
      <div className="margin-top-6">
        <h2 className="font-sans-lg text-bold">{t('interstitialFaqsHeading', 'FAQs')}</h2>
      </div>

      {/* Contact Us */}
      <div className="margin-top-4">
        <h2 className="font-sans-lg text-bold">
          {t('interstitialContactUsHeading', 'Contact Us')}
        </h2>
        <p className="font-sans-sm">
          <a
            href={contactLink}
            target="_blank"
            rel="noopener noreferrer"
            className="usa-link"
          >
            {t('interstitialContactUsLink', 'Need help? Contact us.')}
          </a>
        </p>
      </div>
    </section>
  )
}
