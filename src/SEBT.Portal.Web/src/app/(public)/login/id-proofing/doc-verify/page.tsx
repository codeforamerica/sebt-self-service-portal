import { Suspense } from 'react'

import { DocVerifyPage } from '@/features/auth/components/doc-verify'
import { getState, getStateLinks } from '@sebt/design-system'

function requireSdkKey(): string {
  const key = process.env.NEXT_PUBLIC_SOCURE_SDK_KEY
  if (!key) {
    throw new Error('NEXT_PUBLIC_SOCURE_SDK_KEY environment variable is required')
  }
  return key
}

export default function DocVerifyRoute() {
  const sdkKey = requireSdkKey()
  const state = getState()
  const links = getStateLinks(state)

  return (
    <Suspense>
      <DocVerifyPage
        contactLink={links.external.contactUsAssistance}
        sdkKey={sdkKey}
      />
    </Suspense>
  )
}
