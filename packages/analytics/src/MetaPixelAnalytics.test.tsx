import { render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const { fbq } = vi.hoisted(() => ({
  fbq: vi.fn(() => () => {})
}))

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

import { MetaPixelAnalytics } from './MetaPixelAnalytics'

afterEach(() => {
  scripts.length = 0
  vi.unstubAllGlobals()
})

describe('MetaPixelAnalytics', () => {
  it('inlines the stub snippet so window.fbq() is defined', () => {
    render(<MetaPixelAnalytics pixelId="test-pixel" />)
    const stub = scripts.find((s) => s.id === 'meta-pixel-stub')

    expect(stub?.html).toContain('connect.facebook.net')
  })

  it('calls fbq() with the provided pixel id when ready', () => {
    // Object.defineProperty(window, 'fbq', vi.fn().mockImplementation((event, eventValue) => ({ })));

    render(<MetaPixelAnalytics pixelId="test-pixel" />)

    const cdn = scripts.find((s) => s.id === 'meta-pixel-stub')

    window.fbq = function(event, eventValue) { console.log(`${event} ${eventValue}`) }

    cdn?.onReady?.()

    expect(fbq).toHaveBeenCalledWith('test-pixel')
  })
})
