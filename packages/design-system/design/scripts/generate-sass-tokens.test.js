import { readFileSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { describe, expect, it } from 'vitest'
import {
  SEMANTIC_COMPONENT_TOKENS,
  extractSemanticTokens,
  generateSemanticContent,
  generateSettingsContent,
  processThemeTokens
} from './generate-sass-tokens.js'

const __dirname = dirname(fileURLToPath(import.meta.url))
const loadStateJson = state =>
  JSON.parse(readFileSync(join(__dirname, '..', 'states', `${state}.json`), 'utf8'))

const color = value => ({ $type: 'color', $value: value })

/** A complete semantic token set mirroring the DC mapping. */
const fullSemanticSet = {
  'theme-button-bg': color('{theme-secondary}'),
  'theme-button-bg-hover': color('{theme-secondary-dark}'),
  'theme-button-bg-active': color('{theme-secondary-darker}'),
  'theme-button-text': color('{ink}'),
  'theme-button-outline-bg': color('{white}'),
  'theme-button-outline-border': color('{theme-secondary}'),
  'theme-button-outline-border-hover': color('{theme-secondary-dark}'),
  'theme-button-outline-border-active': color('{theme-secondary-darker}'),
  'theme-button-outline-text': color('{ink}'),
  'theme-button-outline-bg-hover': color('{theme-secondary-lighter}'),
  'theme-button-outline-bg-active': color('{theme-secondary-light}')
}

describe('SEMANTIC_COMPONENT_TOKENS', () => {
  it('lists the 11 button color slots and no USWDS settings', () => {
    expect(SEMANTIC_COMPONENT_TOKENS).toHaveLength(11)
    expect(SEMANTIC_COMPONENT_TOKENS).toContain('theme-button-bg')
    expect(SEMANTIC_COMPONENT_TOKENS).not.toContain('theme-button-border-radius')
  })
})

describe('processThemeTokens', () => {
  it('excludes semantic component tokens from USWDS settings variables', () => {
    const variables = processThemeTokens({
      'theme-primary': color('{cyan-50}'),
      ...fullSemanticSet
    })
    const names = variables.map(v => v.name)
    expect(names).toContain('$theme-color-primary')
    expect(names.filter(n => n.startsWith('$theme-button'))).toEqual([])
  })

  it('still passes theme-button-border-radius through as a USWDS setting', () => {
    const variables = processThemeTokens({
      'theme-button-border-radius': { $type: 'borderRadius', $value: '{pill}' }
    })
    expect(variables.map(v => v.name)).toContain('$theme-button-border-radius')
  })
})

describe('generateSettingsContent', () => {
  it('never emits semantic tokens into the uswds-core settings map', () => {
    const variables = processThemeTokens({
      'theme-primary': color('{cyan-50}'),
      ...fullSemanticSet
    })
    const content = generateSettingsContent('dc', variables, 'test-timestamp')
    expect(content).toContain("$theme-color-primary: 'cyan-50'")
    expect(content).not.toContain('theme-button-bg')
  })
})

describe('extractSemanticTokens', () => {
  it('fails when a state defines zero semantic tokens, listing all 11', () => {
    const zeroTokens = { 'theme-primary': color('{cyan-50}') }
    expect(() => extractSemanticTokens(zeroTokens)).toThrowError(/0 of 11/)
    expect(() => extractSemanticTokens(zeroTokens)).toThrowError(/theme-button-bg/)
    expect(() => extractSemanticTokens(zeroTokens)).toThrowError(/theme-button-outline-bg-active/)
  })

  it('extracts a complete set, stripping the theme- prefix from role references', () => {
    const { variables, missing } = extractSemanticTokens(fullSemanticSet)
    expect(missing).toEqual([])
    const byName = Object.fromEntries(variables.map(v => [v.name, v.value]))
    expect(byName['$theme-button-bg']).toBe("'secondary'")
    expect(byName['$theme-button-bg-hover']).toBe("'secondary-dark'")
    expect(byName['$theme-button-text']).toBe("'ink'")
    expect(byName['$theme-button-outline-bg']).toBe("'white'")
    expect(byName['$theme-button-outline-bg-hover']).toBe("'secondary-lighter'")
  })

  it('keeps system token references unchanged apart from quoting', () => {
    const set = { ...fullSemanticSet, 'theme-button-bg': color('{gold-20v}') }
    const { variables } = extractSemanticTokens(set)
    const bg = variables.find(v => v.name === '$theme-button-bg')
    expect(bg.value).toBe("'gold-20v'")
  })

  it('fails a partial set, naming every missing token', () => {
    const partial = { 'theme-button-bg': color('{theme-secondary}') }
    expect(() => extractSemanticTokens(partial)).toThrowError(/theme-button-bg-hover/)
    expect(() => extractSemanticTokens(partial)).toThrowError(/theme-button-outline-bg-active/)
  })

  it('rejects raw hex values, naming the token and the value', () => {
    const set = { ...fullSemanticSet, 'theme-button-bg': color('#B50909') }
    expect(() => extractSemanticTokens(set)).toThrowError(/theme-button-bg/)
    expect(() => extractSemanticTokens(set)).toThrowError(/#B50909/)
  })
})

describe('generateSemanticContent', () => {
  it('emits one SASS variable per token with a do-not-edit header', () => {
    const { variables } = extractSemanticTokens(fullSemanticSet)
    const content = generateSemanticContent('dc', variables, 'test-timestamp')
    expect(content).toContain('DO NOT EDIT DIRECTLY')
    expect(content).toContain("$theme-button-bg: 'secondary';")
    expect(content).toContain("$theme-button-outline-bg-active: 'secondary-light';")
  })

})

describe('real state token files', () => {
  it.each(['dc', 'co'])('%s.json produces USWDS settings without semantic leakage', state => {
    const stateJson = loadStateJson(state)
    const variables = processThemeTokens(stateJson.theme)
    const names = variables.map(v => v.name)
    expect(names).toContain('$theme-color-secondary')
    expect(names.filter(n => n.startsWith('$theme-button') && n !== '$theme-button-border-radius')).toEqual([])
  })

  it('dc.json defines the complete semantic set following the secondary ladder', () => {
    const { variables, missing } = extractSemanticTokens(loadStateJson('dc').theme)
    expect(missing).toEqual([])
    const byName = Object.fromEntries(variables.map(v => [v.name, v.value]))
    expect(byName['$theme-button-bg']).toBe("'secondary'")
    expect(byName['$theme-button-text']).toBe("'ink'")
    expect(byName['$theme-button-outline-bg-active']).toBe("'secondary-light'")
  })

  it('co.json defines the complete semantic set following the primary ladder', () => {
    const { variables, missing } = extractSemanticTokens(loadStateJson('co').theme)
    expect(missing).toEqual([])
    const byName = Object.fromEntries(variables.map(v => [v.name, v.value]))
    expect(byName['$theme-button-bg']).toBe("'primary'")
    expect(byName['$theme-button-text']).toBe("'white'")
    expect(byName['$theme-button-outline-bg-hover']).toBe("'primary-lightest'")
  })
})
