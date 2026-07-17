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
  const { t: tOffboard } = useTranslation('offBoarding')
  const { t: tCommon } = useTranslation('common')
  const { t: tDev } = useTranslation('dev')

  return (
    <section aria-labelledby="doc-verify-title">
      <h1
        id="doc-verify-title"
        className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
      >
        {tOffboard('title')}
      </h1>

      <p className="font-sans-sm">{tOffboard('body1')}</p>

      <ul className="usa-list font-sans-sm">
        {/* TODO update copy*/}
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
            {tOffboard('action')}
          </Button>
        )}

        <Button
          type="button"
          onClick={onContinue}
          isLoading={isStartingChallenge}
          loadingText={tDev('loading')}
          disabled={isStartingChallenge}
        >
          {tCommon('continue')}
        </Button>
      </div>

      {/* FAQs placeholder */}
      <div className="margin-top-6">
        <h2 className="font-sans-lg text-bold">{tCommon('linkFaqs')}</h2>
      </div>

      {/* Contact Us */}
      <div className="margin-top-4">
        <h2 className="font-sans-lg text-bold">{tCommon('linkContactUs')}</h2>
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
