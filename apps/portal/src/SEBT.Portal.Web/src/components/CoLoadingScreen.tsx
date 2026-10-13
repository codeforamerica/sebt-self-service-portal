'use client'

import {
  getState,
  getStateConfig,
  LoadingInterstitial,
  type LoadingInterstitialProps
} from '@sebt/design-system'

/**
 * Loading interstitial for pages that would otherwise be blank while a request is in flight.
 * Renders only when the state's login mode is OIDC; other states keep the blank screen (DC-337 scope).
 */
export function CoLoadingScreen({ title, message }: LoadingInterstitialProps) {
  if (getStateConfig(getState()).loginMode !== 'oidc') return null
  return (
    <div className="grid-container maxw-tablet">
      <LoadingInterstitial
        title={title}
        message={message}
      />
    </div>
  )
}
