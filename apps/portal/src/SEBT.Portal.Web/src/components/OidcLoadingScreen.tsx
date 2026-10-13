'use client'

import {
  getState,
  getStateConfig,
  LoadingInterstitial,
  type LoadingInterstitialProps
} from '@sebt/design-system'

/**
 * Loading interstitial for pages that would otherwise be blank while a request is in flight.
 * Renders only for states that enable it; the rest keep the blank screen they shipped with.
 */
export function OidcLoadingScreen({ title, message }: LoadingInterstitialProps) {
  if (!getStateConfig(getState()).showLoadingInterstitial) return null
  return (
    <div className="grid-container maxw-tablet">
      <LoadingInterstitial
        title={title}
        message={message}
      />
    </div>
  )
}
