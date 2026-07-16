import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

let mockState = 'co'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => mockState
  }
})

import { CoLoadingScreen } from './CoLoadingScreen'

describe('CoLoadingScreen', () => {
  beforeEach(() => {
    mockState = 'co'
  })

  it('renders the loading interstitial when state is CO', () => {
    render(
      <CoLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Please wait...')
    expect(screen.getByText('Do not exit the page.')).toBeInTheDocument()
  })

  it('renders nothing when state is not CO', () => {
    mockState = 'dc'
    const { container } = render(
      <CoLoadingScreen
        title="Please wait..."
        message="Do not exit the page."
      />
    )

    expect(container).toBeEmptyDOMElement()
  })
})
