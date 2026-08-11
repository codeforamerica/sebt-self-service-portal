import { beforeEach, describe, expect, it } from 'vitest'

import { clearReplacementFlash, getReplacementFlash, setReplacementFlash } from './replacementFlash'

const CARD = { childFirstName: 'Jane', childLastName: 'Doe', ebtCardLastFour: '1234' }

describe('replacementFlash', () => {
  beforeEach(() => {
    clearReplacementFlash()
  })

  it('round-trips cards through set and get', () => {
    setReplacementFlash([CARD])
    expect(getReplacementFlash()).toEqual([CARD])
  })

  it('get is non-destructive so a StrictMode double read still sees the cards', () => {
    setReplacementFlash([CARD])
    getReplacementFlash()
    expect(getReplacementFlash()).toEqual([CARD])
  })

  it('returns a copy that does not expose the internal array', () => {
    setReplacementFlash([CARD])
    getReplacementFlash().pop()
    expect(getReplacementFlash()).toHaveLength(1)
  })

  it('set replaces any previously stored cards', () => {
    setReplacementFlash([CARD])
    setReplacementFlash([{ childFirstName: 'John', childLastName: 'Roe', ebtCardLastFour: null }])
    expect(getReplacementFlash()).toEqual([
      { childFirstName: 'John', childLastName: 'Roe', ebtCardLastFour: null }
    ])
  })

  it('clear empties the store', () => {
    setReplacementFlash([CARD])
    clearReplacementFlash()
    expect(getReplacementFlash()).toEqual([])
  })
})
