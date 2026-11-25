import { spawnSync } from 'child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

const script = join(dirname(fileURLToPath(import.meta.url)), 'generate-backend-email.js')

const HEADER = 'Content,🟢 CO English Current,🟢 CO Español Current'
const SHARED_HEADER = 'Content,Shared English Current,Shared Español Current'
const emailRows = (prefix) => [
  `S8 - OTP Email Message - Sender,${prefix} Program,${prefix} Programa`,
  `S8 - OTP Email Message - Subject,${prefix} Subject,${prefix} Asunto`,
  `S8 - OTP Email Message - Body 1,${prefix} Body 1,${prefix} Cuerpo 1`,
  `S8 - OTP Email Message - Body 3,${prefix} Body 3,${prefix} Cuerpo 3`,
]

let dir

function writeStateCsv(state, rows) {
  writeFileSync(join(dir, 'states', `${state}.csv`), [HEADER, ...rows].join('\n'))
}

function writeSharedCsv(rows) {
  writeFileSync(join(dir, 'shared.csv'), [SHARED_HEADER, ...rows].join('\n'))
}

function run(states) {
  return spawnSync(
    'node',
    [script, '--csv-dir', join(dir, 'states'), '--out-dir', join(dir, 'out'), '--states', states],
    { encoding: 'utf8' }
  )
}

const readEmail = (state) => JSON.parse(readFileSync(join(dir, 'out', `EmailContent.${state}.json`), 'utf8'))

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), 'backend-email-'))
  mkdirSync(join(dir, 'states'))
})

afterEach(() => {
  rmSync(dir, { recursive: true, force: true })
})

describe('generate-backend-email.js', () => {
  it('writes email content from a state CSV when there is no shared content', () => {
    writeStateCsv('co', emailRows('CO'))

    const result = run('co')

    expect(result.status).toBe(0)
    expect(readEmail('co')).toEqual({
      en: { programName: 'CO Program', subject: 'CO Subject', body1: 'CO Body 1', body3: 'CO Body 3' },
      es: { programName: 'CO Programa', subject: 'CO Asunto', body1: 'CO Cuerpo 1', body3: 'CO Cuerpo 3' },
    })
  })

  it('skips a state whose English cell is !N/A!', () => {
    writeStateCsv('co', ['S8 - OTP Email Message - Subject,!N/A!,!N/A!'])

    run('co')

    expect(existsSync(join(dir, 'out', 'EmailContent.co.json'))).toBe(false)
  })

  it('gives a state with no CSV the shared email content', () => {
    writeSharedCsv(emailRows('Shared'))

    const result = run('demo')

    expect(result.status).toBe(0)
    expect(readEmail('demo').en.subject).toBe('Shared Subject')
  })

  it('lets a state row override shared per key, so a state !N/A! removes the email', () => {
    writeSharedCsv(emailRows('Shared'))
    writeStateCsv('co', ['S8 - OTP Email Message - Subject,!N/A!,!N/A!'])
    writeStateCsv('dc', ['S8 - OTP Email Message - Subject,DC Subject,DC Asunto'])

    run('co,dc')

    expect(existsSync(join(dir, 'out', 'EmailContent.co.json'))).toBe(false)
    expect(readEmail('dc').en).toEqual({
      programName: 'Shared Program',
      subject: 'DC Subject',
      body1: 'Shared Body 1',
      body3: 'Shared Body 3',
    })
  })

  it('fails for a state with neither a CSV nor shared content', () => {
    const result = run('demo')

    expect(result.status).not.toBe(0)
    expect(result.stderr).toContain('demo')
  })
})
