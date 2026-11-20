import { spawnSync } from 'child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { dirname, join, relative } from 'path'
import { fileURLToPath } from 'url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

const script = join(dirname(fileURLToPath(import.meta.url)), 'generate-locales.js')

const MARKER = '!N/A!'

// The shape of a "Ready to publish" tab export: the key, then one column per language.
const CLEAN_HEADER = ['Variable Name/Key', '🟡 DC English Current', '🟡 DC Español Current', '🟡 DC Amharic Current']
const TITLE = ['S1 - Landing Page - Title', 'Get Summer EBT', 'Obtén Summer EBT', 'ሰመር ኢቢቲ']
const CONTINUE = ['GLOBAL - Button Continue', 'Continue', 'Continuar', 'ቀጥል']
const HEADING = ['S7 - Portal Dashboard - Heading', 'Dashboard', 'Panel', 'ዳሽቦርድ']
const CLEAN_ROWS = [TITLE, CONTINUE, HEADING]

// The shape of a "WORKING" tab export: the same copy between columns the generator must ignore.
const WORKING_HEADER = [
  'Variable Name/Key',
  'Figma Preview URL',
  '🟡 DC English Current',
  '⚪ SOURCE English',
  '🟡 DC Español Current',
  '⚪ SOURCE Español',
  '🟡 DC Amharic Current',
  'Status',
  'Notes'
]
const WORKING_ROWS = CLEAN_ROWS.map(([key, en, es, am]) => [
  key,
  'https://example.test/figma',
  en,
  'source English',
  es,
  'source Español',
  am,
  'Ready to Publish',
  'a note'
])

let dir
let runCount

function toCsv(rows) {
  const cell = (value) => (/[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value)
  return rows.map((row) => row.map(cell).join(',')).join('\n')
}

function generate(rows, { runDir = join(dir, `run-${runCount++}`), args = [] } = {}) {
  mkdirSync(join(runDir, 'states'), { recursive: true })
  writeFileSync(join(runDir, 'states', 'dc.csv'), toCsv(rows))

  const result = spawnSync(
    'node',
    [
      script,
      '--csv-dir', join(runDir, 'states'),
      '--out-dir', join(runDir, 'locales'),
      '--ts-out', join(runDir, 'resources.ts'),
      '--app', 'portal',
      ...args
    ],
    { encoding: 'utf8' }
  )
  return { ...result, runDir, localesDir: join(runDir, 'locales') }
}

/** Every generated locale file, keyed by its path under the output directory. */
function localesFrom(rows) {
  const { status, stderr, localesDir } = generate(rows)
  expect(status, stderr).toBe(0)

  const files = readdirSync(localesDir, { recursive: true }).filter((file) => file.endsWith('.json'))
  return Object.fromEntries(
    files.map((file) => [
      relative(localesDir, join(localesDir, file)),
      JSON.parse(readFileSync(join(localesDir, file), 'utf8'))
    ])
  )
}

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), 'csv-shape-'))
  runCount = 0
})

afterEach(() => {
  rmSync(dir, { recursive: true, force: true })
})

describe('a key-plus-Current-columns export', () => {
  it('generates each language from its Current column', () => {
    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS])

    expect(locales['en/dc/landing.json']).toEqual({ title: 'Get Summer EBT' })
    expect(locales['es/dc/common.json']).toEqual({ buttonContinue: 'Continuar' })
    expect(locales['am/dc/dashboard.json']).toEqual({ heading: 'ዳሽቦርድ' })
  })

  it('gives the same output as an export that carries the working columns', () => {
    expect(localesFrom([WORKING_HEADER, ...WORKING_ROWS])).toEqual(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS]))
  })

  it('gives the same output whatever order the columns are in', () => {
    // Reversed, each SOURCE column comes before the Current column for its language.
    const reversed = [WORKING_HEADER, ...WORKING_ROWS].map((row) => [...row].reverse())

    expect(localesFrom(reversed)).toEqual(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS]))
  })

  it('keeps a paragraph break and a quotation mark inside a cell', () => {
    const body = ['S1 - Landing Page - Body', 'Most children are enrolled.\n\nTap "Apply now".', 'cuerpo', 'አካል']

    const locales = localesFrom([CLEAN_HEADER, TITLE, body])

    expect(locales['en/dc/landing.json'].body).toBe('Most children are enrolled.\n\nTap "Apply now".')
  })
})

describe('an export whose key column has no header', () => {
  // A "Ready to publish" tab exports the key column under a blank header cell.
  const blankKeyHeader = [' ', '🟡 DC English Current', '🟡 DC Español Current', '🟡 DC Amharic Current']

  it('reads the first column as the key', () => {
    expect(localesFrom([blankKeyHeader, ...CLEAN_ROWS])).toEqual(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS]))
  })

  it('passes --validate', () => {
    const { status, stderr } = generate([blankKeyHeader, ...CLEAN_ROWS], { args: ['--validate'] })

    expect(status, stderr).toBe(0)
  })

  it('still reads a labelled key column that is not first', () => {
    const keyLast = [[...blankKeyHeader.slice(1), 'Variable Name/Key'], ...CLEAN_ROWS.map(([key, ...copy]) => [...copy, key])]

    expect(localesFrom(keyLast)).toEqual(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS]))
  })
})

describe('two pages that write to one namespace', () => {
  const header = ['Variable Name/Key', '🟡 DC English Current']
  const whoTitle = ['S5 - Bulk Order New Cards Who - Title', 'Which cards need to be replaced?']
  const bulkConfirmTitle = ['S5 - Bulk Order New Cards Confirm - Title', 'A few things to know']
  const confirmAction = ['S6 - Confirm Request Replacement Card - Action', 'Order card']
  const fisAction = ['S6 - Request Replacement Card FIS - Action', 'Go to the dashboard']

  it.each([
    ['select page first', [whoTitle, bulkConfirmTitle]],
    ['confirm page first', [bulkConfirmTitle, whoTitle]]
  ])('keep the card selection and bulk confirm titles apart, %s', (_order, rows) => {
    const optionalId = localesFrom([header, ...rows])['en/dc/optionalId.json']

    expect(optionalId).toEqual({ title: 'Which cards need to be replaced?', confirmTitle: 'A few things to know' })
  })

  it.each([
    ['confirm page first', [confirmAction, fisAction]],
    ['card office page first', [fisAction, confirmAction]]
  ])('keep the single-card order button its own key, %s', (_order, rows) => {
    const result = localesFrom([header, ...rows])['en/dc/result.json']

    expect(result.confirmAction).toBe('Order card')
  })
})

describe('a row marked as not applying to the state', () => {
  const marked = ['S7 - Portal Dashboard - Case Number', MARKER, MARKER, MARKER]

  it('leaves its key out of every language', () => {
    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, marked])

    for (const locale of ['en', 'es', 'am']) {
      expect(locales[`${locale}/dc/dashboard.json`]).not.toHaveProperty('caseNumber')
    }
  })

  it('gives the same output as deleting the row from the sheet', () => {
    expect(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, marked])).toEqual(localesFrom([CLEAN_HEADER, ...CLEAN_ROWS]))
  })
})

describe('a cell marked as not applying, next to live English', () => {
  const spanishMarked = ['S7 - Portal Dashboard - Footnote', 'See the back of your card', MARKER, 'ካርድ']
  const amharicMarked = ['S7 - Portal Dashboard - Footnote', 'See the back of your card', 'Mira el reverso', MARKER]

  it('leaves the key out of Spanish only, so Spanish falls back to English', () => {
    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, spanishMarked])

    expect(locales['en/dc/dashboard.json'].footnote).toBe('See the back of your card')
    expect(locales['am/dc/dashboard.json'].footnote).toBe('ካርድ')
    expect(locales['es/dc/dashboard.json']).not.toHaveProperty('footnote')
  })

  it('leaves the key out of Amharic only', () => {
    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, amharicMarked])

    expect(locales['es/dc/dashboard.json'].footnote).toBe('Mira el reverso')
    expect(locales['am/dc/dashboard.json']).not.toHaveProperty('footnote')
  })

  it('never writes the marker into a locale file', () => {
    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, spanishMarked, amharicMarked])

    expect(JSON.stringify(locales)).not.toContain(MARKER)
  })

  it('keeps the translation an earlier row gave the same key', () => {
    const earlier = ['S7 - Portal Dashboard - Footnote', 'Old English', 'Mira el reverso', 'ካርድ']

    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, earlier, spanishMarked])

    expect(locales['en/dc/dashboard.json'].footnote).toBe('See the back of your card')
    expect(locales['es/dc/dashboard.json'].footnote).toBe('Mira el reverso')
  })

  it('takes the translation a later row gives the same key', () => {
    const later = ['S7 - Portal Dashboard - Footnote', 'New English', 'Texto nuevo', 'ካርድ']

    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, spanishMarked, later])

    expect(locales['es/dc/dashboard.json'].footnote).toBe('Texto nuevo')
  })
})

describe('behaviour a marked translation cell must not change', () => {
  it('still writes a blank translation cell as an empty string', () => {
    const untranslated = ['S7 - Portal Dashboard - Footnote', 'See the back of your card', '', 'ካርድ']

    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, untranslated])

    expect(locales['es/dc/dashboard.json'].footnote).toBe('')
  })

  it('still drops the whole row when English is marked, authored translations included', () => {
    const englishMarked = ['S7 - Portal Dashboard - Footnote', MARKER, 'Mira el reverso', 'ካርድ']

    const locales = localesFrom([CLEAN_HEADER, ...CLEAN_ROWS, englishMarked])

    for (const locale of ['en', 'es', 'am']) {
      expect(locales[`${locale}/dc/dashboard.json`]).not.toHaveProperty('footnote')
    }
  })
})

describe('a CSV whose header lacks a required column', () => {
  // The language columns are there, but without the word the generator looks for.
  const noCurrent = [['Variable Name/Key', '🟡 DC English', '🟡 DC Español', '🟡 DC Amharic'], TITLE]

  function expectOneLineNaming(stderr, ...fragments) {
    expect(stderr.trim().split('\n')).toHaveLength(1)
    for (const fragment of fragments) {
      expect(stderr).toContain(fragment)
    }
  }

  it('fails a generate run with one line that says what is required and what it found', () => {
    const { status, stderr } = generate(noCurrent)

    expect(status).toBe(1)
    expectOneLineNaming(stderr, 'dc.csv', 'no "English Current" column', 'Required:', 'Optional:', '🟡 DC English')
  })

  it('fails --validate with the same line', () => {
    const { status, stderr } = generate(noCurrent, { args: ['--validate'] })

    expect(status).toBe(1)
    expectOneLineNaming(stderr, 'dc.csv', 'no "English Current" column', 'Required:', 'Optional:', '🟡 DC English')
  })

  it('names the key column when that is the one missing', () => {
    const { status, stderr } = generate([['Key', '🟡 DC English Current'], ['S1 - Landing Page - Title', 'Get Summer EBT']])

    expect(status).toBe(1)
    expectOneLineNaming(stderr, 'no "Variable Name/Key" column', 'first column with a blank header')
  })

  it('names both columns for an empty file', () => {
    const { status, stderr } = generate([])

    expect(status).toBe(1)
    expectOneLineNaming(stderr, 'no "Variable Name/Key" or "English Current" column')
  })

  it('leaves the locale files from the last good run in place', () => {
    const { runDir, localesDir } = generate([CLEAN_HEADER, ...CLEAN_ROWS])
    const landing = join(localesDir, 'en', 'dc', 'landing.json')
    expect(existsSync(landing)).toBe(true)

    const { status } = generate(noCurrent, { runDir })

    expect(status).toBe(1)
    expect(existsSync(landing)).toBe(true)
  })
})

describe('a CSV with only the required columns', () => {
  it('generates English and no other language', () => {
    const locales = localesFrom([
      ['Variable Name/Key', '🟢 CO English Current'],
      ['S1 - Landing Page - Title', 'Get Summer EBT']
    ])

    expect(Object.keys(locales)).toEqual(['en/dc/landing.json'])
  })
})
