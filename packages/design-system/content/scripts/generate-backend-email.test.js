import { spawnSync } from 'child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

const script = join(dirname(fileURLToPath(import.meta.url)), 'generate-backend-email.js')

const LANGUAGES = ['🟡 DC English Current', '🟡 DC Español Current']
const EMAIL_ROWS = [
  ['S8 - OTP Email Message - Sender', 'DC SUN Bucks', 'DC SUN Bucks'],
  ['S8 - OTP Email Message - Subject', 'Your code', 'Tu código'],
  ['S8 - OTP Email Message - Body 1', 'Use this code', 'Usa este código'],
  ['S8 - OTP Email Message - Body 3', 'It expires soon', 'Vence pronto']
]

let dir

/** Runs the generator on one state CSV and returns its exit status and the file it wrote, if any. */
function emailContentFrom(header) {
  const statesDir = join(dir, 'states')
  const outDir = join(dir, 'out')
  mkdirSync(statesDir, { recursive: true })
  writeFileSync(join(statesDir, 'dc.csv'), [header, ...EMAIL_ROWS].map((row) => row.join(',')).join('\n'))

  const { status, stderr } = spawnSync('node', [script, '--csv-dir', statesDir, '--out-dir', outDir], {
    encoding: 'utf8'
  })
  const outPath = join(outDir, 'EmailContent.dc.json')
  return { status, stderr, content: existsSync(outPath) ? JSON.parse(readFileSync(outPath, 'utf8')) : null }
}

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), 'email-content-'))
})

afterEach(() => {
  rmSync(dir, { recursive: true, force: true })
})

describe('backend email content', () => {
  it('writes each language from a CSV with a labelled key column', () => {
    const { status, stderr, content } = emailContentFrom(['Variable Name/Key', ...LANGUAGES])

    expect(status, stderr).toBe(0)
    expect(content).toEqual({
      en: { programName: 'DC SUN Bucks', subject: 'Your code', body1: 'Use this code', body3: 'It expires soon' },
      es: { programName: 'DC SUN Bucks', subject: 'Tu código', body1: 'Usa este código', body3: 'Vence pronto' }
    })
  })

  it('writes the same content when the key column has no header', () => {
    const labelled = emailContentFrom(['Variable Name/Key', ...LANGUAGES])
    expect(labelled.content).not.toBeNull()
    rmSync(join(dir, 'out'), { recursive: true, force: true })

    const { status, stderr, content } = emailContentFrom([' ', ...LANGUAGES])

    expect(status, stderr).toBe(0)
    expect(content).toEqual(labelled.content)
  })
})
