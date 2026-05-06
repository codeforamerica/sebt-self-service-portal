import { render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const { initMixpanelBridge } = vi.hoisted(() => ({
  initMixpanelBridge: vi.fn(() => () => {})
}))
vi.mock('@sebt/analytics', () => ({ initMixpanelBridge }))

// Capture the props next/script receives so we can assert the wiring without
// actually executing Mixpanel's boot snippet (which appends real <script>
// tags to the DOM and is brittle under React's strict-mode double render).
const scriptProps: {
  html?: string | undefined
  onReady?: (() => void) | undefined
} = {}
vi.mock('next/script', () => ({
  default: ({
    dangerouslySetInnerHTML,
    onReady
  }: {
    dangerouslySetInnerHTML?: { __html: string }
    onReady?: () => void
  }) => {
    scriptProps.html = dangerouslySetInnerHTML?.__html
    scriptProps.onReady = onReady
    return null
  }
}))

import { MixpanelAnalytics } from './MixpanelAnalytics'

afterEach(() => {
  initMixpanelBridge.mockClear()
  scriptProps.html = undefined
  scriptProps.onReady = undefined
})

describe('MixpanelAnalytics', () => {
  it('inlines the Mixpanel boot snippet so window.mixpanel is defined before the CDN lib loads', () => {
    render(<MixpanelAnalytics token="test-token" />)

    // The CDN script alone never defines `window.mixpanel`. Without the snippet
    // pre-defining the queue stub, the bridge silently no-ops and any mixpanel.*
    // call hits "object not initialized".
    expect(scriptProps.html).toContain('window.mixpanel=a')
    expect(scriptProps.html).toContain('cdn.mxpnl.com/libs/mixpanel-2-latest.min.js')
  })

  it('calls initMixpanelBridge with the provided token after the snippet is ready', () => {
    render(<MixpanelAnalytics token="test-token" />)
    scriptProps.onReady?.()

    expect(initMixpanelBridge).toHaveBeenCalledWith('test-token')
  })

  it('does not initialize the bridge before the snippet has executed', () => {
    render(<MixpanelAnalytics token="test-token" />)
    expect(initMixpanelBridge).not.toHaveBeenCalled()
  })
})
