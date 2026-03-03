import { OffBoardingPage } from '@/features/auth/components/off-boarding'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'

export default function OffBoardingRoute() {
  const state = getState()
  const links = getStateLinks(state)

  return <OffBoardingPage contactLink={links.external.contactUsAssistance} />
}
