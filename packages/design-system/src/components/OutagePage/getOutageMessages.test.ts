import { afterEach, describe, expect, it, vi } from 'vitest'

import type { StateResources } from '../../lib/i18n'
import { getOutageFooterCopy, getOutageMessages } from './getOutageMessages'

// CO supports en + es, so fixtures use CO to keep language expectations explicit.
vi.stubEnv('NEXT_PUBLIC_STATE', 'co')

afterEach(() => {
  vi.unstubAllEnvs()
  vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
})

function resourcesWith(outage: {
  en?: Record<string, string>
  es?: Record<string, string>
}): StateResources {
  return {
    co: {
      en: outage.en ? { outage: outage.en } : {},
      es: outage.es ? { outage: outage.es } : {}
    }
  }
}

describe('getOutageMessages', () => {
  it('returns outage copy for each supported language in the given resources', () => {
    const messages = getOutageMessages(
      resourcesWith({
        en: { body1: 'Down for maintenance.', body2: 'Come back soon.' },
        es: { body1: 'En mantenimiento.', body2: 'Vuelva pronto.' }
      })
    )

    expect(messages).toEqual([
      { language: 'en', body1: 'Down for maintenance.', body2: 'Come back soon.' },
      { language: 'es', body1: 'En mantenimiento.', body2: 'Vuelva pronto.' }
    ])
  })

  it('omits languages with no outage copy so the page only stacks real content', () => {
    const messages = getOutageMessages(
      resourcesWith({ en: { body1: 'Down for maintenance.', body2: 'Come back soon.' } })
    )

    expect(messages).toHaveLength(1)
    expect(messages[0]?.language).toBe('en')
  })

  it('treats whitespace-only copy as missing', () => {
    const messages = getOutageMessages(resourcesWith({ en: { body1: '   ', body2: '' } }))

    expect(messages).toHaveLength(0)
  })

  it('returns empty when the state bundle is absent entirely', () => {
    expect(getOutageMessages({})).toHaveLength(0)
  })
})

describe('getOutageFooterCopy', () => {
  it('returns configured footer prefixes per language', () => {
    const copy = getOutageFooterCopy(
      resourcesWith({
        en: { footer: 'For help, see' },
        es: { footer: 'Para ayuda, vea' }
      })
    )

    expect(copy).toEqual([
      { language: 'en', prefix: 'For help, see' },
      { language: 'es', prefix: 'Para ayuda, vea' }
    ])
  })

  it('falls back to default prefixes when footer copy is missing', () => {
    const copy = getOutageFooterCopy({})

    expect(copy).toEqual([
      { language: 'en', prefix: 'For more information, visit' },
      { language: 'es', prefix: 'Para más información, visite' }
    ])
  })
})
