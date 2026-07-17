import { render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

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
})

describe('MetaPixelAnalytics', () => {
  it('inlines the stub snippet so window.fbq() is defined', () => {
    render(<MetaPixelAnalytics pixelId="test-pixel" />)

    const stub = scripts.find((s) => s.id === 'meta-pixel-stub')

    expect(stub?.html).toContain('connect.facebook.net')
    expect(stub?.html).toContain("fbq('init','test-pixel')")
  })
})
