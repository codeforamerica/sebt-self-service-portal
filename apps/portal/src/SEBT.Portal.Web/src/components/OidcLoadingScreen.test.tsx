import type { LoginMode, StateCode } from '@sebt/design-system'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

let mockLoginMode: LoginMode = 'oidc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getStateConfig: (state: StateCode) => ({
      ...actual.getStateConfig(state),
      loginMode: mockLoginMode
    })
  }
})

import { OidcLoadingScreen } from './OidcLoadingScreen'

describe('OidcLoadingScreen', () => {
  beforeEach(() => {
    mockLoginMode = 'oidc'
  })

  it('renders the loading interstitial when the login mode is OIDC', () => {
    render(
      <OidcLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Please wait...')
    expect(screen.getByText('Do not exit the page.')).toBeInTheDocument()
  })

  it('renders nothing when the login mode is OTP', () => {
    mockLoginMode = 'otp'
    const { container } = render(
      <OidcLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(container).toBeEmptyDOMElement()
  })
})
