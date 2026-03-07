import { DocVerifyPage } from '@/features/auth/components/doc-verify'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'

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
    <DocVerifyPage
      contactLink={links.external.contactUsAssistance}
      sdkKey={sdkKey}
    />
  )
}
