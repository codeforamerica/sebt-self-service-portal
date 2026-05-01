import { afterEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return { ...actual, getState: () => mockState }
})

afterEach(() => {
  mockState = 'dc'
})

describe('getApplyHref', () => {
  it('returns the CO PEAK starting-page URL when state is co', () => {
    mockState = 'co'
    expect(getApplyHref()).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns /apply when state is dc', () => {
    mockState = 'dc'
    expect(getApplyHref()).toBe('/apply')
  })

  it('falls back to /apply for an unknown state', () => {
    mockState = 'xx'
    expect(getApplyHref()).toBe('/apply')
  })
})
