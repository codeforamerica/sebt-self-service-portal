import { render } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mockTeardown = vi.fn()
const mockInit = vi.fn().mockReturnValue(mockTeardown)
vi.mock('@sebt/analytics', () => ({
  initSiteImproveBridge: () => mockInit()
}))

vi.mock('next/script', () => ({
  default: ({ onLoad, src, nonce }: { onLoad?: () => void; src: string; nonce?: string }) => {
    onLoad?.()
    return (
      // Mock stub for Next.js Script — the rule below fires for runtime usage,
      // not for a render-only test double.
      // eslint-disable-next-line @next/next/no-sync-scripts
      <script
        data-testid="siteimprove-script"
        src={src}
        {...(nonce ? { nonce } : {})}
      />
    )
  }
}))

import { SiteImproveAnalytics } from './SiteImproveAnalytics'

describe('SiteImproveAnalytics', () => {
  beforeEach(() => {
    mockInit.mockClear()
    mockTeardown.mockClear()
    mockInit.mockReturnValue(mockTeardown)
  })

  it('renders a script tag pointing at the SiteImprove CDN with the encoded site id', () => {
    const { getByTestId } = render(<SiteImproveAnalytics siteId="123456" />)

    const script = getByTestId('siteimprove-script') as HTMLScriptElement
    expect(script.getAttribute('src')).toBe(
      'https://siteimproveanalytics.com/js/siteanalyze_123456.js'
    )
  })

  it('forwards the nonce to the script tag for CSP compliance', () => {
    const { getByTestId } = render(
      <SiteImproveAnalytics
        siteId="123456"
        nonce="test-nonce"
      />
    )

    expect(getByTestId('siteimprove-script').getAttribute('nonce')).toBe('test-nonce')
  })

  it('initializes the bridge once when the script loads', () => {
    render(<SiteImproveAnalytics siteId="123456" />)
    expect(mockInit).toHaveBeenCalledTimes(1)
  })

  it('invokes the teardown function on unmount', () => {
    const { unmount } = render(<SiteImproveAnalytics siteId="123456" />)

    expect(mockTeardown).not.toHaveBeenCalled()
    unmount()
    expect(mockTeardown).toHaveBeenCalledTimes(1)
  })

  it('encodes the site id in the script src to prevent path injection', () => {
    const { getByTestId } = render(<SiteImproveAnalytics siteId="abc/../../evil" />)

    const src = getByTestId('siteimprove-script').getAttribute('src')!
    expect(src).not.toContain('../')
    expect(src).toBe('https://siteimproveanalytics.com/js/siteanalyze_abc%2F..%2F..%2Fevil.js')
  })
})
