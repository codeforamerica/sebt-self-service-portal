import { DocVerifyPage } from '@/features/auth/components/doc-verify'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'

// TODO: Replace with actual SDK key from environment variable once Socure sandbox is available
const SOCURE_SDK_KEY = process.env.NEXT_PUBLIC_SOCURE_SDK_KEY ?? 'mock-sdk-key'

export default function DocVerifyRoute() {
  const state = getState()
  const links = getStateLinks(state)

  return (
    <DocVerifyPage
      contactLink={links.external.contactUsAssistance}
      sdkKey={SOCURE_SDK_KEY}
    />
  )
}
