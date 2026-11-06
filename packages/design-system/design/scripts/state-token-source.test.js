import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import { resolveStateTokens } from './state-token-source.js'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const designSystemRoot = resolve(scriptDir, '../..')
const repoRoot = resolve(designSystemRoot, '../..')

describe('resolveStateTokens', () => {
  let externalDir

  beforeEach(() => {
    externalDir = mkdtempSync(join(tmpdir(), 'sebt-external-tokens-'))
    writeFileSync(join(externalDir, 'co.json'), '{"theme":{}}')
  })

  afterEach(() => {
    rmSync(externalDir, { recursive: true, force: true })
  })

  it('uses the in-repo token file when no external directory is configured', () => {
    expect(resolveStateTokens('co', {})).toEqual({
      path: join(designSystemRoot, 'design', 'states', 'co.json'),
      label: 'design/states/co.json',
      external: false
    })
  })

  it('uses the directory named by EXTERNAL_TOKENS_DIR_{STATE} when it is set', () => {
    expect(resolveStateTokens('co', { EXTERNAL_TOKENS_DIR_CO: externalDir })).toEqual({
      path: join(externalDir, 'co.json'),
      label: '$EXTERNAL_TOKENS_DIR_CO/co.json',
      external: true
    })
  })

  it('only applies a state external directory to that state', () => {
    const source = resolveStateTokens('dc', { EXTERNAL_TOKENS_DIR_CO: externalDir })

    expect(source.external).toBe(false)
    expect(source.path).toBe(join(designSystemRoot, 'design', 'states', 'dc.json'))
  })

  it('resolves a relative external directory against the repository root', () => {
    const relativeDir = relative(repoRoot, externalDir)

    expect(resolveStateTokens('co', { EXTERNAL_TOKENS_DIR_CO: relativeDir }).path).toBe(
      join(externalDir, 'co.json')
    )
  })

  it('treats an empty external directory as not configured', () => {
    expect(resolveStateTokens('co', { EXTERNAL_TOKENS_DIR_CO: '' }).external).toBe(false)
  })

  it('throws, naming the variable, when the external directory has no file for the state', () => {
    const emptyDir = join(externalDir, 'empty')
    mkdirSync(emptyDir)

    expect(() => resolveStateTokens('co', { EXTERNAL_TOKENS_DIR_CO: emptyDir })).toThrow(
      /EXTERNAL_TOKENS_DIR_CO is set, but co\.json was not found/
    )
  })
})

describe('token scripts read through resolveStateTokens', () => {
  const portalRoot = resolve(repoRoot, 'apps/portal/src/SEBT.Portal.Web')
  let workDir

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'sebt-token-scripts-'))
  })

  afterEach(() => {
    rmSync(workDir, { recursive: true, force: true })
  })

  // Outputs are pointed at workDir where a script allows it, in case one runs past the error.
  const scripts = [
    { script: 'generate-tokens.js' },
    { script: 'generate-sass-tokens.js', env: () => ({ SASS_OUTPUT_DIR: workDir }) },
    { script: 'generate-fonts.js', cwd: () => workDir },
    { script: 'generate-theme-css.js', cwd: () => portalRoot },
    { script: 'generate-token-types.js', args: ['co'] },
    { script: 'validate-tokens.js', args: ['co'] }
  ]

  for (const { script, args = [], env = () => ({}), cwd = () => designSystemRoot } of scripts) {
    it(`${script} fails rather than using the in-repo file when the external file is missing`, () => {
      const result = spawnSync('node', [join(scriptDir, script), ...args], {
        cwd: cwd(),
        env: { ...process.env, STATE: 'co', EXTERNAL_TOKENS_DIR_CO: workDir, ...env() },
        encoding: 'utf8',
        timeout: 60_000
      })

      expect(result.status).toBe(1)
      expect(result.stdout + result.stderr).toContain('EXTERNAL_TOKENS_DIR_CO is set, but co.json was not found')
    })
  }
})
