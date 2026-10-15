import { strict as assert } from 'node:assert'
import { spawnSync } from 'node:child_process'
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { after, describe, it } from 'node:test'

const SCRIPT = join(import.meta.dirname, 'with-doppler.sh')
const PROJECT = 'safety-net-sebt-self-service-portal'
const SYSTEM_PATH = '/usr/bin:/bin'

// Stand-in for the Doppler CLI: reports a configured project and marks commands it runs.
const FAKE_DOPPLER = `#!/bin/sh
if [ "$1 $2 $3" = "configure get project" ]; then echo "$FAKE_DOPPLER_PROJECT"; exit 0; fi
if [ "$1 $2 $3" = "configure get config" ]; then echo "local_personal"; exit 0; fi
if [ "$1" = "run" ] && [ "$2" = "--" ]; then
  if [ -n "$FAKE_DOPPLER_RUN_ERROR" ]; then echo "$FAKE_DOPPLER_RUN_ERROR" >&2; exit 1; fi
  shift 2
  INJECTED_BY_DOPPLER=yes exec "$@"
fi
exit 99
`

const fakeDopplerDir = mkdtempSync(join(tmpdir(), 'fake-doppler-'))
writeFileSync(join(fakeDopplerDir, 'doppler'), FAKE_DOPPLER)
chmodSync(join(fakeDopplerDir, 'doppler'), 0o755)
after(() => rmSync(fakeDopplerDir, { recursive: true, force: true }))

interface RunOptions {
  useDoppler?: string
  dopplerInstalled?: boolean
  configuredProject?: string
  runError?: string
  command?: string
}

function runWrapper({
  useDoppler,
  dopplerInstalled = true,
  configuredProject = PROJECT,
  runError = '',
  command = 'echo "doppler=${INJECTED_BY_DOPPLER:-no}"',
}: RunOptions = {}) {
  const env: NodeJS.ProcessEnv = {
    PATH: dopplerInstalled ? `${fakeDopplerDir}:${SYSTEM_PATH}` : SYSTEM_PATH,
    FAKE_DOPPLER_PROJECT: configuredProject,
    FAKE_DOPPLER_RUN_ERROR: runError,
  }
  if (useDoppler !== undefined) env.USE_DOPPLER = useDoppler
  const result = spawnSync('/bin/bash', [SCRIPT, 'sh', '-c', command], { env, encoding: 'utf8' })
  return { status: result.status, stdout: result.stdout, stderr: result.stderr }
}

describe('with-doppler.sh', () => {
  it('runs the command under Doppler when USE_DOPPLER is unset and the repo is set up', () => {
    const result = runWrapper()
    assert.equal(result.status, 0)
    assert.match(result.stdout, /doppler=yes/)
    assert.match(result.stdout, /Using Doppler: safety-net-sebt-self-service-portal \/ local_personal/)
  })

  it('runs the command directly when Doppler is not installed', () => {
    const result = runWrapper({ dopplerInstalled: false })
    assert.equal(result.status, 0)
    assert.match(result.stdout, /doppler=no/)
    assert.match(result.stdout, /using local config files/)
  })

  it('runs the command directly when Doppler is set up for a different project', () => {
    const result = runWrapper({ configuredProject: 'some-other-project' })
    assert.equal(result.status, 0)
    assert.match(result.stdout, /doppler=no/)
  })

  it('runs the command directly when Doppler has no project for this directory', () => {
    const result = runWrapper({ configuredProject: '' })
    assert.equal(result.status, 0)
    assert.match(result.stdout, /doppler=no/)
  })

  for (const value of ['false', 'FALSE', '0']) {
    it(`skips Doppler when USE_DOPPLER=${value} even if the repo is set up`, () => {
      const result = runWrapper({ useDoppler: value })
      assert.equal(result.status, 0)
      assert.match(result.stdout, /doppler=no/)
    })
  }

  for (const value of ['true', 'TRUE', '1']) {
    it(`runs under Doppler when USE_DOPPLER=${value} and the repo is set up`, () => {
      const result = runWrapper({ useDoppler: value })
      assert.equal(result.status, 0)
      assert.match(result.stdout, /doppler=yes/)
    })
  }

  it('fails with setup instructions when USE_DOPPLER=true but Doppler is not set up', () => {
    const result = runWrapper({ useDoppler: 'true', dopplerInstalled: false })
    assert.equal(result.status, 1)
    assert.doesNotMatch(result.stdout, /doppler=/)
    assert.match(result.stderr, /doppler setup/)
  })

  it('rejects an unrecognized USE_DOPPLER value without running the command', () => {
    const result = runWrapper({ useDoppler: 'maybe' })
    assert.equal(result.status, 1)
    assert.doesNotMatch(result.stdout, /doppler=/)
    assert.match(result.stderr, /USE_DOPPLER/)
  })

  it('fails loudly, without running the command, when Doppler cannot load secrets', () => {
    const result = runWrapper({ runError: 'Doppler Error: Unable to fetch secrets' })
    assert.equal(result.status, 1)
    assert.doesNotMatch(result.stdout, /doppler=/)
    assert.match(result.stderr, /Unable to fetch secrets/)
    assert.match(result.stderr, /USE_DOPPLER=false/)
  })

  it('passes through the exit code of the wrapped command', () => {
    const result = runWrapper({ command: 'exit 7' })
    assert.equal(result.status, 7)
  })
})
