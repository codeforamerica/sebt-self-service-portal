import { afterEach, describe, expect, it } from 'vitest'

import {
  clearCachedOutageFlag,
  readCachedOutageFlag,
  writeCachedOutageFlag
} from './outageFlagCache'

describe('outageFlagCache', () => {
  afterEach(() => {
    clearCachedOutageFlag()
  })

  it('returns null when nothing is cached', () => {
    expect(readCachedOutageFlag()).toBeNull()
  })

  it('round-trips true and false values', () => {
    writeCachedOutageFlag(true)
    expect(readCachedOutageFlag()).toBe(true)

    writeCachedOutageFlag(false)
    expect(readCachedOutageFlag()).toBe(false)
  })

  it('clears cached values', () => {
    writeCachedOutageFlag(true)
    clearCachedOutageFlag()
    expect(readCachedOutageFlag()).toBeNull()
  })
})
