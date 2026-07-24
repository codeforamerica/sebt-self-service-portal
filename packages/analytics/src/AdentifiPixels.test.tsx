import { render } from '@testing-library/react'
import { beforeAll, afterEach, describe, expect, it, vi } from 'vitest'
import { AdentifiPixels } from './AdentifiPixels'

let originalLocation

beforeAll(() => {
  originalLocation = window.location;
});

afterEach(() => {
  vi.restoreAllMocks(); // Clean up mock states
});

describe('AdentifiPixels', () => {
  it('returns empty tag when href is empty', () => {
    vi.spyOn(window, 'location', 'get').mockImplementation(() => ({
      ...originalLocation,
      href: null,
    }));

    const { container } = render(<AdentifiPixels pixelId="test-pixel" />)

    expect(container.querySelector('img')).toBeNull()
  })

  it('returns img tag with nonce', () => {
    const { container } = render(<AdentifiPixels pixelId="test-pixel" />)

    const img = container.querySelector('img')

    expect(img).not.toBeNull()

    expect(img.src).toContain('p_url=')
    expect(img.src).toContain('=test-pixel')
  })
})
