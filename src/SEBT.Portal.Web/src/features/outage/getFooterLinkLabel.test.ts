import { describe, expect, it } from 'vitest'

import { getFooterLinkLabel } from './getFooterLinkLabel'

describe('getFooterLinkLabel', () => {
  it('returns host and path for http URLs', () => {
    expect(getFooterLinkLabel('https://sunbucks.dc.gov/page/contact-us')).toBe(
      'sunbucks.dc.gov/page/contact-us'
    )
  })

  it('returns the email address for mailto links without the mailto prefix', () => {
    expect(getFooterLinkLabel('mailto:support@example.com')).toBe('support@example.com')
  })

  it('returns the href unchanged for other schemes', () => {
    expect(getFooterLinkLabel('#')).toBe('#')
  })
})
