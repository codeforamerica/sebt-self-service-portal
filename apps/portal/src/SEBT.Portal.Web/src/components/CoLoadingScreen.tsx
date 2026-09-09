'use client'

import { getState, LoadingInterstitial, type LoadingInterstitialProps } from '@sebt/design-system'

/**
 * CO-only loading screen for portal pages where the user would otherwise see
 * a blank screen while a request is in flight (auth status, household data,
 * sign-in callback). Renders nothing in non-CO states so existing behavior is
 * preserved per DC-337's CO-only scope.
 */
export function CoLoadingScreen({ title, message }: LoadingInterstitialProps) {
  if (getState() !== 'co') return null
  return (
    <div className="grid-container maxw-tablet">
      <LoadingInterstitial
        title={title}
        message={message}
      />
    </div>
  )
}
