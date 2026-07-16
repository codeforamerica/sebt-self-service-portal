import { Suspense } from 'react'

import { DocVerifyPage } from '@/features/auth/components/doc-verify'
import { getState, getStateLinks } from '@sebt/design-system'

export default function DocVerifyRoute() {
  const state = getState()
  const links = getStateLinks(state)

  return (
    <Suspense>
      <DocVerifyPage contactLink={links.external.contactUsAssistance} />
    </Suspense>
  )
}
