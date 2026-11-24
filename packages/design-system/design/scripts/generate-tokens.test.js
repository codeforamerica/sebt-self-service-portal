import { mkdtempSync, rmSync, utimesSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import { needsRegeneration } from './generate-tokens.js'

describe('needsRegeneration', () => {
  let dir
  let input
  let output

  const writeAt = (path, content, seconds) => {
    writeFileSync(path, content)
    utimesSync(path, seconds, seconds)
  }

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'sebt-tokens-cache-'))
    input = join(dir, 'co.json')
    output = join(dir, 'tokens.css')
  })

  afterEach(() => {
    rmSync(dir, { recursive: true, force: true })
  })

  it('regenerates when there is no output yet', () => {
    writeAt(input, '{}', 1000)

    expect(needsRegeneration(input, output, 'design/states/co.json')).toBe(true)
  })

  it('regenerates when the token file is newer than the output', () => {
    writeAt(output, ' * Source: design/states/co.json (theme object only)', 1000)
    writeAt(input, '{}', 2000)

    expect(needsRegeneration(input, output, 'design/states/co.json')).toBe(true)
  })

  it('reuses output that is newer than the token file and came from the same source', () => {
    writeAt(input, '{}', 1000)
    writeAt(output, ' * Source: design/states/co.json (theme object only)', 2000)

    expect(needsRegeneration(input, output, 'design/states/co.json')).toBe(false)
  })

  it('regenerates when the output came from a different source, even if it is newer', () => {
    writeAt(input, '{}', 1000)
    writeAt(output, ' * Source: design/states/co.json (theme object only)', 2000)

    expect(needsRegeneration(input, output, '$EXTERNAL_TOKENS_DIR_CO/co.json')).toBe(true)
  })

  it('regenerates when the output was generated for a different state', () => {
    writeAt(input, '{}', 1000)
    writeAt(output, ' * Source: design/states/dc.json (theme object only)', 2000)

    expect(needsRegeneration(input, output, 'design/states/co.json')).toBe(true)
  })
})
