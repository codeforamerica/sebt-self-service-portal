/**
 * Guards the shape of each web app's copy:* scripts. The generator's argument
 * list (app, sections, output paths) must live in one script, copy:locales,
 * that copy:generate and copy:validate both call. If either repeats the
 * arguments, a section added to one is silently missing from the other.
 */
import { readFileSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { describe, expect, it } from 'vitest'

const appsDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'apps', 'portal', 'src')

const apps = ['SEBT.Portal.Web', 'SEBT.EnrollmentChecker.Web']

function scriptsOf(app) {
  return JSON.parse(readFileSync(join(appsDir, app, 'package.json'), 'utf8')).scripts
}

describe.each(apps)('%s copy scripts', (app) => {
  const scripts = scriptsOf(app)

  it('runs the generator only from copy:locales', () => {
    expect(scripts['copy:locales']).toContain('generate-locales.js')
    expect(scripts['copy:locales']).not.toContain('--validate')
    expect(scripts['copy:generate']).not.toContain('generate-locales.js')
    expect(scripts['copy:validate']).not.toContain('generate-locales.js')
  })

  it('derives copy:generate from copy:locales', () => {
    expect(scripts['copy:generate']).toContain('copy:locales')
    expect(scripts['copy:generate']).not.toContain('--validate')
  })

  it('derives copy:validate from copy:locales with the --validate flag', () => {
    expect(scripts['copy:validate']).toContain('copy:locales')
    expect(scripts['copy:validate']).toContain('--validate')
  })
})

it('portal copy:generate still produces the backend email templates', () => {
  expect(scriptsOf('SEBT.Portal.Web')['copy:generate']).toContain('generate-backend-email.js')
})
