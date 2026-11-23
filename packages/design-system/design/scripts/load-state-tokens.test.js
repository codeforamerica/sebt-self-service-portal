import { readFileSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { describe, expect, it } from 'vitest'
import { loadStateTokens, tokenSourcePaths } from './load-state-tokens.js'

const designDir = join(dirname(fileURLToPath(import.meta.url)), '..')
const readJson = file => JSON.parse(readFileSync(join(designDir, file), 'utf8'))

describe('loadStateTokens', () => {
  const shared = readJson('shared.json')

  it('returns the shared tokens for a state with no file', () => {
    expect(tokenSourcePaths('no-such-state')).toEqual([join(designDir, 'shared.json')])
    expect(loadStateTokens('no-such-state')).toEqual(shared)
  })

  it('lets a state value replace the shared value', () => {
    expect(shared.theme['theme-primary'].$value).toBe('{cyan-50}')
    expect(loadStateTokens('dc').theme['theme-primary'].$value).toBe('{cyan-60}')
  })

  it('keeps shared tokens the state does not mention, and the shared system group', () => {
    const merged = loadStateTokens('co')
    expect(merged.theme['theme-global-link-styles']).toEqual(shared.theme['theme-global-link-styles'])
    expect(merged.system).toEqual(shared.system)
  })

  it('reproduces each state theme the shared base was trimmed from', () => {
    expect(loadStateTokens('co').theme['theme-font-type-sans'].$value).toBe('Atkinson Hyperlegible')
    expect(loadStateTokens('dc').theme['theme-font-type-sans'].$value).toBe('Urbanist')
    expect(loadStateTokens('dc').theme['theme-button-bg'].$value).toBe('{theme-secondary}')
    expect(loadStateTokens('co').theme['theme-button-bg'].$value).toBe('{theme-primary}')
  })
})
