import type { StateCode } from '@sebt/design-system'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

let mockShowLoadingInterstitial = true
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getStateConfig: (state: StateCode) => ({
      ...actual.getStateConfig(state),
      showLoadingInterstitial: mockShowLoadingInterstitial
    })
  }
})

import { CoLoadingScreen } from './CoLoadingScreen'

describe('CoLoadingScreen', () => {
  beforeEach(() => {
    mockShowLoadingInterstitial = true
  })

  it('renders the loading interstitial when the state enables it', () => {
    render(
      <CoLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Please wait...')
    expect(screen.getByText('Do not exit the page.')).toBeInTheDocument()
  })

  it('renders nothing when the state keeps the blank loading screen', () => {
    mockShowLoadingInterstitial = false
    const { container } = render(
      <CoLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(container).toBeEmptyDOMElement()
  })
})
