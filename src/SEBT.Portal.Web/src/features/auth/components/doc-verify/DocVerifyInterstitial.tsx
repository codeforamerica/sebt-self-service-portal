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
        {t('offBoarding:title')}
      </h1>

      <p className="font-sans-sm">{t('offBoarding:body1')}</p>

      <ul className="usa-list font-sans-sm">
        <li>{t('interstitialIdTypeDriversLicense')}</li>
        <li>{t('interstitialIdTypeForeignPassport')}</li>
        <li>{t('interstitialIdTypeOtherPhotoId')}</li>
      </ul>

      {allowIdRetry && <p className="font-sans-sm">{t('offBoarding:body3')}</p>}

      <div className="margin-top-3">
        {allowIdRetry && (
          <Button
            type="button"
            className="usa-button--outline margin-right-2"
            onClick={onEnterIdNumber}
          >
            {t('interstitialActionEnterId')}
          </Button>
        )}

        <Button
          type="button"
          onClick={onContinue}
          isLoading={isStartingChallenge}
          loadingText={t('interstitialLoading')}
          disabled={isStartingChallenge}
        >
          {t('common:continue')}
        </Button>
      </div>

      {/* FAQs placeholder */}
      <div className="margin-top-6">
        <h2 className="font-sans-lg text-bold">{t('common:linkFaqs')}</h2>
      </div>

      {/* Contact Us */}
      <div className="margin-top-4">
        <h2 className="font-sans-lg text-bold">{t('common:linkContactUs')}</h2>
        <p className="font-sans-sm">
          <a
            href={contactLink}
            target="_blank"
            rel="noopener noreferrer"
            className="usa-link"
          >
            {t('interstitialContactUsLink')}
          </a>
        </p>
      </div>
    </section>
  )
}
