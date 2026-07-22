import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AdentifiPixels } from './AdentifiPixels'

describe('AdentifiPixels', () => {
  it('returns img tag with nonce', () => {
    const { container } = render(<AdentifiPixels pixelId="test-pixel" />)

    const img = container.querySelector('img')

    expect(img).not.toBeNull()

    expect(img.src).toContain('p_url=')
    expect(img.src).toContain('=test-pixel')
  })
})
