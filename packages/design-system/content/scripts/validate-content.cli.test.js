import { spawnSync } from 'child_process'
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

const script = join(dirname(fileURLToPath(import.meta.url)), 'generate-locales.js')

const HEADER = 'Content,🟡 DC English Current,🟡 DC Español Current,🟡 DC Amharic Current'
const CLEAN_ROWS = [
  'GLOBAL - Button Continue,Continue,Continuar,ቀጥል',
  '"S7 - Portal Dashboard - Alert Title","Cards ending in [[9999], [9999],] will be sent","Tarjetas terminadas en [[9999], [9999],] serán enviadas","ካርድ [[9999], [9999],]"'
]
// The generator treats output as cached only when each locale has a landing.json.
const LANDING_ROW = 'S1 - Landing Page - Title,Get Summer EBT,Obtén Summer EBT,ሰመር ኢቢቲ'
// No code renders this key. The defect that reached production: one opening
// brace lost from "{{name}}".
const BRACE_ROW = 'S7 - Portal Dashboard - Greeting,Hello {name}},Hola {{name}},ሰላም {{name}}'
// The Spanish cell is empty.
const UNTRANSLATED_ROW = 'S7 - Portal Dashboard - Footnote,See the back of your card,,ካርድ'
// Spanish lost the closing bracket of the example list.
const BROKEN_ROW =
  '"S7 - Portal Dashboard - Alert Title","Cards ending in [[9999], [9999],] will be sent","Tarjetas terminadas en [[9999], [9999], serán enviadas","ካርድ [[9999], [9999],]"'

let dir

function writeCsv(rows) {
  writeFileSync(join(dir, 'states', 'dc.csv'), [HEADER, ...rows].join('\n'))
}

function run(extraArgs = []) {
  return spawnSync(
    'node',
    [
      script,
      '--csv-dir', join(dir, 'states'),
      '--out-dir', join(dir, 'locales'),
      '--ts-out', join(dir, 'resources.ts'),
      '--app', 'portal',
      ...extraArgs
    ],
    { encoding: 'utf8' }
  )
}

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), 'validate-content-'))
  mkdirSync(join(dir, 'states'), { recursive: true })
})

afterEach(() => {
  rmSync(dir, { recursive: true, force: true })
})

describe('generate-locales.js --validate', () => {
  it('exits 0 and writes nothing for clean content', () => {
    writeCsv(CLEAN_ROWS)

    const result = run(['--validate'])

    expect(result.status).toBe(0)
    expect(existsSync(join(dir, 'locales'))).toBe(false)
    expect(existsSync(join(dir, 'resources.ts'))).toBe(false)
  })

  it('exits 1 and names the state, locale, key, and rule for a content error', () => {
    writeCsv([CLEAN_ROWS[0], BROKEN_ROW])

    const result = run(['--validate'])

    expect(result.status).toBe(1)
    expect(result.stderr).toContain('unbalanced-brackets')
    expect(result.stderr).toContain('dc/es dashboard.alertTitle')
  })

  it('exits 1 for a defect in a key that no app code renders', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])

    const result = run(['--validate'])

    expect(result.status).toBe(1)
    expect(result.stderr).toContain('mismatched-braces')
    expect(result.stderr).toContain('dc/en dashboard.greeting')
  })

  it('warns about a missing translation without failing', () => {
    writeCsv([...CLEAN_ROWS, UNTRANSLATED_ROW])

    const result = run(['--validate'])

    expect(result.status).toBe(0)
    expect(result.stdout).toContain('Missing Spanish translation in dc: dashboard.footnote')
  })

  it('does not announce a generate run', () => {
    writeCsv(CLEAN_ROWS)

    const result = run(['--validate'])

    expect(result.stdout).not.toContain('Generating')
  })

  it('still validates when the generated output is cached', () => {
    writeCsv([LANDING_ROW, BROKEN_ROW])
    run() // populates the cache
    expect(run().stdout).toContain('Locales unchanged (cached)')

    const result = run(['--validate'])

    expect(result.status).toBe(1)
  })
})

describe('generate-locales.js without --validate', () => {
  it('reports a content error but still generates and exits 0', () => {
    writeCsv([CLEAN_ROWS[0], BROKEN_ROW])

    const result = run()

    expect(result.status).toBe(0)
    expect(result.stderr).toContain('unbalanced-brackets')
    expect(existsSync(join(dir, 'locales', 'es', 'dc', 'dashboard.json'))).toBe(true)
  })

  it('reports the content error again on a cached run', () => {
    writeCsv([LANDING_ROW, BROKEN_ROW])
    run() // populates the cache

    const result = run()

    expect(result.status).toBe(0)
    expect(result.stdout).toContain('Locales unchanged (cached)')
    expect(result.stderr).toContain('unbalanced-brackets')
  })
})
