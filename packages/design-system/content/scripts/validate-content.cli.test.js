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

  it('still validates when the generated output is cached', () => {
    writeCsv([CLEAN_ROWS[0], BROKEN_ROW])
    run() // populates the cache

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
})

describe('generate-locales.js --validate --src-dirs', () => {
  // The defect that reached production: one opening brace lost from "{{name}}".
  const BRACE_ROW = 'S7 - Portal Dashboard - Greeting,Hello {name}},Hola {{name}},ሰላም {{name}}'
  const RENDERS_GREETING = `const { t } = useTranslation('dashboard')\nexport const Page = () => <h1>{t('greeting')}</h1>\n`
  const RENDERS_SOMETHING_ELSE = `const { t } = useTranslation('dashboard')\nexport const Page = () => <h1>{t('title')}</h1>\n`

  function writeSource(folder, file, text) {
    mkdirSync(join(dir, folder), { recursive: true })
    writeFileSync(join(dir, folder, file), text)
  }

  it('exits 1 when the app renders the key with the "{text}}" defect', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])
    writeSource('src', 'Page.tsx', RENDERS_GREETING)

    const result = run(['--validate', '--src-dirs', join(dir, 'src')])

    expect(result.status).toBe(1)
    expect(result.stderr).toContain('mismatched-braces')
    expect(result.stderr).toContain('dc/en dashboard.greeting')
  })

  it('exits 0 and warns when no app code references the defective key', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])
    writeSource('src', 'Page.tsx', RENDERS_SOMETHING_ELSE)

    const result = run(['--validate', '--src-dirs', join(dir, 'src')])

    expect(result.status).toBe(0)
    expect(result.stdout).toContain('mismatched-braces')
    expect(result.stdout).toContain('no app code references this key')
  })

  it('reads every directory in a comma-separated list', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])
    writeSource('src', 'Page.tsx', RENDERS_SOMETHING_ELSE)
    writeSource('shared', 'Greeting.tsx', RENDERS_GREETING)

    const result = run(['--validate', '--src-dirs', `${join(dir, 'src')},${join(dir, 'shared')}`])

    expect(result.status).toBe(1)
  })

  it('does not count a reference that only a test file makes', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])
    writeSource('src', 'Page.tsx', RENDERS_SOMETHING_ELSE)
    writeSource('src', 'Page.test.tsx', RENDERS_GREETING)

    const result = run(['--validate', '--src-dirs', join(dir, 'src')])

    expect(result.status).toBe(0)
  })

  it('fails instead of guessing when a source directory does not exist', () => {
    writeCsv([CLEAN_ROWS[0], BRACE_ROW])

    const result = run(['--validate', '--src-dirs', join(dir, 'missing')])

    expect(result.status).toBe(1)
    expect(result.stderr).toContain('--src-dirs')
  })
})
