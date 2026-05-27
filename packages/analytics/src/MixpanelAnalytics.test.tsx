import { render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const { initMixpanelBridge } = vi.hoisted(() => ({
  initMixpanelBridge: vi.fn(() => () => {})
}))
vi.mock('./mixpanel-bridge', () => ({ initMixpanelBridge }))

// Capture props from both Script elements so we can assert the wiring without
// making real network requests or executing the boot snippet in jsdom.
type ScriptRecord = { id?: string; src?: string; html?: string; onReady?: () => void }
const scripts: ScriptRecord[] = []
vi.mock('next/script', () => ({
  default: ({
    id,
    src,
    dangerouslySetInnerHTML,
    onReady
  }: {
    id?: string
    src?: string
    dangerouslySetInnerHTML?: { __html: string }
    onReady?: () => void
  }) => {
    scripts.push({ id, src, html: dangerouslySetInnerHTML?.__html, onReady })
    return null
  }
}))

import { MixpanelAnalytics } from './MixpanelAnalytics'

afterEach(() => {
  initMixpanelBridge.mockClear()
  scripts.length = 0
})

describe('MixpanelAnalytics', () => {
  it('inlines the stub snippet so window.mixpanel is defined before the CDN executes', () => {
    render(<MixpanelAnalytics token="test-token" />)
    const stub = scripts.find((s) => s.id === 'mixpanel-stub')

    expect(stub?.html).toContain('window.mixpanel=a')
    // CDN URL must NOT be in the stub — it is loaded by the separate src Script
    expect(stub?.html).not.toContain('cdn.mxpnl.com')
  })

  it('loads the Mixpanel CDN as a src script so onReady fires reliably', () => {
    render(<MixpanelAnalytics token="test-token" />)
    const cdn = scripts.find((s) => s.id === 'mixpanel-cdn')

    expect(cdn?.src).toContain('cdn.mxpnl.com/libs/mixpanel-2-latest.min.js')
  })

  it('calls initMixpanelBridge with the provided token after the CDN is ready', () => {
    render(<MixpanelAnalytics token="test-token" />)
    const cdn = scripts.find((s) => s.id === 'mixpanel-cdn')
    cdn?.onReady?.()

    expect(initMixpanelBridge).toHaveBeenCalledWith('test-token')
  })

  it('does not initialize the bridge before the CDN has loaded', () => {
    render(<MixpanelAnalytics token="test-token" />)
    expect(initMixpanelBridge).not.toHaveBeenCalled()
  })
})
