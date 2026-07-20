import { describe, expect, it } from 'vitest'

import { getFooterLinkLabel } from './getFooterLinkLabel'

describe('getFooterLinkLabel', () => {
  it('shows host and path for http(s) URLs', () => {
    expect(getFooterLinkLabel('https://sunbucks.dc.gov/page/contact-us')).toBe(
      'sunbucks.dc.gov/page/contact-us'
    )
  })

  it('drops a bare trailing slash path', () => {
    expect(getFooterLinkLabel('https://cdhs.colorado.gov/')).toBe('cdhs.colorado.gov')
  })

  it('shows the email address only for mailto links', () => {
    expect(getFooterLinkLabel('mailto:help@example.com')).toBe('help@example.com')
  })

  it('passes through non-http, non-mailto values unchanged', () => {
    expect(getFooterLinkLabel('#')).toBe('#')
  })
})
