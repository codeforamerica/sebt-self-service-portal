import { describe, expect, it } from 'vitest'

import { hasMarkdown, staleExemptions } from './i18nContentScan'

describe('staleExemptions', () => {
  const appStates = ['dc', 'co']

  it('reports an exemption whose content has returned for every exempted state', () => {
    const exempt = [{ key: 'login:body', states: ['co'], reason: 'DC-only branch' }]
    const referenced = new Set(['login:body'])
    const live = new Set<string>()

    expect(staleExemptions(exempt, referenced, live, appStates)).toEqual(exempt)
  })

  it('keeps an exemption while the key is still unresolved in an exempted state', () => {
    const exempt = [{ key: 'login:body', states: ['co'], reason: 'DC-only branch' }]
    const referenced = new Set(['login:body'])
    const live = new Set(['login:body|co'])

    expect(staleExemptions(exempt, referenced, live, appStates)).toEqual([])
  })

  it('keeps an exemption while any exempted state is still unresolved', () => {
    const exempt = [{ key: 'dashboard:apply', states: ['dc', 'co'], reason: 'latent' }]
    const referenced = new Set(['dashboard:apply'])
    const live = new Set(['dashboard:apply|co'])

    expect(staleExemptions(exempt, referenced, live, appStates)).toEqual([])
  })

  it('ignores an exemption for a key this app never asks for', () => {
    const exempt = [{ key: 'checker:closed.title', states: ['co'], reason: 'checker only' }]
    const referenced = new Set(['login:body'])
    const live = new Set<string>()

    expect(staleExemptions(exempt, referenced, live, appStates)).toEqual([])
  })

  it('ignores an exemption whose states this app does not ship', () => {
    const exempt = [{ key: 'login:body', states: ['dc'], reason: 'portal only' }]
    const referenced = new Set(['login:body'])
    const live = new Set<string>()

    expect(staleExemptions(exempt, referenced, live, ['co'])).toEqual([])
  })
})

describe('hasMarkdown', () => {
  it('matches bold in Latin script', () => {
    expect(hasMarkdown('Sign in with **your account**')).toBe(true)
  })

  it('matches bold in a non-Latin script', () => {
    expect(hasMarkdown('የተመዘገቡ ተማሪዎች **አንድ የ120 ዶላር ክፍያ** አግኝተዋል')).toBe(true)
  })

  it('matches a markdown link', () => {
    expect(hasMarkdown('See [the FAQ](https://example.gov/faq) for details')).toBe(true)
  })

  it('does not match a masked value made of asterisks', () => {
    expect(hasMarkdown('***-**-6789')).toBe(false)
  })

  it('does not match plain copy', () => {
    expect(hasMarkdown('Tap here to contact us [Required]')).toBe(false)
  })
})
