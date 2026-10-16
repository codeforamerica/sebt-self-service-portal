'use client'

import { getState, getStateConfig, type LoginMode, type StateCode } from '@sebt/design-system'
import { COLoginPage } from './COLoginPage'
import { OtpLoginPage } from './OtpLoginPage'

const loginPages: Record<LoginMode, React.ComponentType<{ state: StateCode }>> = {
  oidc: COLoginPage,
  otp: OtpLoginPage
}

export default function LoginPage() {
  const state = getState()
  const { loginMode } = getStateConfig(state)

  // eslint-disable-next-line security/detect-object-injection -- loginMode is typed LoginMode
  const Page = loginPages[loginMode]
  return <Page state={state} />
}
