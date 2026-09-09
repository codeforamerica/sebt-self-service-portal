import { describe, expect, it } from 'vitest'

import { getStateLinks } from './links'

// Pins the shared link values the maintenance page's action buttons depend on.
// The button labels are literal URLs, so the hrefs must match the copy exactly.
describe('getStateLinks', () => {
  it('provides the DC state site matching the "Go to sunbucks.dc.gov" label', () => {
    expect(getStateLinks('dc').help.sebtMainSite).toBe('https://sunbucks.dc.gov')
  })

  it('provides the CO state site matching the "Go to cdhs.colorado.gov/summer-ebt" label', () => {
    expect(getStateLinks('co').help.sebtMainSite).toBe('https://cdhs.colorado.gov/summer-ebt')
  })

  it.each(['dc', 'co'] as const)('provides a non-empty %s contact destination', (state) => {
    expect(getStateLinks(state).help.contactUs).toBeTruthy()
  })

  it('routes the CO contact destination to the help desk mailbox', () => {
    const co = getStateLinks('co').help
    expect(co.contactUs).toBe(co.helpDeskEmail)
  })
})
