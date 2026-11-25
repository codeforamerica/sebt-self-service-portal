import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { join } from 'path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import {
  loadStateRows,
  locateContent,
  mergeContentRows,
  normalizeRows,
  parseCSV,
  resolveStates,
  sharedCsvPathFor,
} from './merge-content.js'
import { STATE_CODES } from '../../src/lib/state.ts'

const row = (key, en, es, am) => ({ key, en, es, am })

describe('normalizeRows', () => {
  it('detects the key and "current" language columns by header', () => {
    const rows = normalizeRows(
      parseCSV(
        [
          'Variable Name/Key,🟡 DC English Current,⚪ SOURCE English,🟡 DC Español Current,🟡 DC Amharic Current',
          'GLOBAL - Button,Go,Source,Ir,ሂድ',
        ].join('\n')
      ),
      'dc'
    )

    expect(rows).toEqual([row('GLOBAL - Button', 'Go', 'Ir', 'ሂድ')])
  })

  it('leaves a language undefined when the CSV has no column for it, and empty when the cell is blank', () => {
    const rows = normalizeRows(
      parseCSV(['Content,Shared English Current,Shared Español Current', 'GLOBAL - Button,Go,'].join('\n')),
      'shared content'
    )

    expect(rows).toEqual([row('GLOBAL - Button', 'Go', '', undefined)])
  })

  it('skips rows without a key', () => {
    const rows = normalizeRows(
      parseCSV(['Content,English Current', ',orphan', 'GLOBAL - Button,Go'].join('\n')),
      'dc'
    )

    expect(rows.map((r) => r.key)).toEqual(['GLOBAL - Button'])
  })

  it('throws when the key or English column is missing', () => {
    expect(() => normalizeRows(parseCSV('Foo,Bar\nx,y'), 'shared content')).toThrow(/shared content/)
  })
})

describe('mergeContentRows', () => {
  it('keeps shared rows the state does not override', () => {
    const merged = mergeContentRows([row('A - B', 'shared', 'compartido')], [row('A - C', 'own', 'propio')])

    expect(merged.map((r) => r.key).sort()).toEqual(['A - B', 'A - C'])
  })

  it('replaces the shared row entirely, even with empty or ignored values', () => {
    const shared = [row('A - B', 'x', 'x-es', 'x-am'), row('A - C', 'y', 'y-es', 'y-am')]
    const state = [row('A - B', '', '', undefined), row('A - C', '!N/A!', '', undefined)]

    expect(mergeContentRows(shared, state)).toEqual(state)
  })

  it('matches keys that differ only in case or spacing', () => {
    const state = [row('a - b  c', '', '', undefined)]

    expect(mergeContentRows([row('A - B C', 'x', 'x-es', undefined)], state)).toEqual(state)
  })

  it('matches keys with the given key function', () => {
    const state = [row('S1 - Page - Title', '', '', undefined)]
    const byLastPart = (r) => r.key.split(' - ').pop()

    expect(mergeContentRows([row('S9 - Other - Title', 'x', 'x-es', undefined)], state, byLastPart)).toEqual(state)
  })

  it('is the state rows unchanged when there is no shared content', () => {
    const state = [row('A - B', 'x', 'y', undefined), row('A - B', 'dup', 'dup', undefined)]

    expect(mergeContentRows([], state)).toEqual(state)
  })
})

describe('locateContent and loadStateRows', () => {
  let dir
  let statesDir

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'merge-content-'))
    statesDir = join(dir, 'states')
    mkdirSync(statesDir)
  })

  afterEach(() => {
    rmSync(dir, { recursive: true, force: true })
  })

  const header = 'Content,English Current,Español Current'

  it('puts shared.csv beside the states directory', () => {
    expect(sharedCsvPathFor(statesDir)).toBe(join(dir, 'shared.csv'))
  })

  it('fails for a state with neither a CSV nor shared content', () => {
    expect(() => locateContent(statesDir, ['demo'])).toThrow(/demo/)
  })

  it('allows a state with no CSV when shared content exists', () => {
    writeFileSync(join(dir, 'shared.csv'), `${header}\nA - B,Hi,Hola`)

    const located = locateContent(statesDir, ['demo'])

    expect(located.states).toEqual([{ state: 'demo', csvPath: null }])
    expect(loadStateRows(located.states[0], located.sharedPath)).toEqual([row('A - B', 'Hi', 'Hola', undefined)])
  })

  it('reports no shared path when shared.csv is absent', () => {
    writeFileSync(join(statesDir, 'dc.csv'), `${header}\nA - B,Hi,Hola`)

    expect(locateContent(statesDir, ['dc']).sharedPath).toBeNull()
  })
})

describe('resolveStates', () => {
  it('defaults to the registered state codes', () => {
    expect(resolveStates(null)).toEqual([...STATE_CODES])
  })

  it('accepts a comma-separated override', () => {
    expect(resolveStates('co, Demo')).toEqual(['co', 'demo'])
  })
})
