import { describe, expect, it } from 'vitest'

import { getOutageFooterCopy, getOutageMessages } from '@/features/outage/getOutageMessages'

describe('getOutageMessages', () => {
  it('returns outage copy for each supported language in the current state bundle', () => {
    const messages = getOutageMessages()

    expect(messages.length).toBeGreaterThan(0)
    expect(messages.every((message) => message.body1.length > 0)).toBe(true)
    expect(messages.every((message) => message.body2.length > 0)).toBe(true)
  })

  it('returns English footer prefix copy', () => {
    expect(getOutageFooterCopy().prefix.length).toBeGreaterThan(0)
  })
})
