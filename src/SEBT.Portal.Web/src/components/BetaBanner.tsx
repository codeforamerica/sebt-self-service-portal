'use client'

import { useFeatureFlag } from '@/features/feature-flags'
import { Alert } from '@sebt/design-system'
import { Copy } from '@sebt/design-system/client'

export function BetaBanner() {
  const enabled = useFeatureFlag('enable_beta_banner')

  if (!enabled) {
    return null
  }

  return (
    <Alert
      variant="warning"
      className="margin-top-0"
    >
      <Copy
        ns="common"
        k="alertBeta"
        fallback="This site is currently in beta. Some features may be incomplete or missing."
      />
    </Alert>
  )
}
