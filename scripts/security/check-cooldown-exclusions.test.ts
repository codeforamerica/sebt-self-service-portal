import { strict as assert } from 'node:assert'
import { describe, it } from 'node:test'
import { checkExclusions } from './check-cooldown-exclusions.ts'

const lock = (...keys: string[]) => new Set(keys)

describe('checkExclusions', () => {
  it('passes when there are no exclusion entries', () => {
    const result = checkExclusions({ minimumReleaseAgeExclude: [] }, lock())
    assert.equal(result.ok, true)
    assert.deepEqual(result.staleEntries, [])
  })

  it('passes when the field is absent', () => {
    const result = checkExclusions({}, lock())
    assert.equal(result.ok, true)
  })

  it('passes when a version-pinned exclusion still resolves in the lockfile', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['next@16.2.6'] },
      lock('next@16.2.6'),
    )
    assert.equal(result.ok, true)
    assert.deepEqual(result.staleEntries, [])
  })

  it('fails when a version-pinned exclusion no longer resolves in the lockfile', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['next@16.2.6'] },
      lock('next@16.2.7'),
    )
    assert.equal(result.ok, false)
    assert.deepEqual(result.staleEntries, ['next@16.2.6'])
  })

  it('handles scoped package names with @-prefix correctly', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['@next/env@16.2.6', '@next/swc-darwin-arm64@16.2.6'] },
      lock('@next/env@16.2.6', '@next/swc-darwin-arm64@16.2.6'),
    )
    assert.equal(result.ok, true)
  })

  it('reports a stale scoped exclusion', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['@next/env@16.2.6'] },
      lock('@next/env@16.2.7'),
    )
    assert.equal(result.ok, false)
    assert.deepEqual(result.staleEntries, ['@next/env@16.2.6'])
  })

  it('passes a disjunction exclusion when any pinned version resolves', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['webpack@4.47.0 || 5.102.1'] },
      lock('webpack@5.102.1'),
    )
    assert.equal(result.ok, true)
  })

  it('fails a disjunction exclusion when no pinned version resolves', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['webpack@4.47.0 || 5.102.1'] },
      lock('webpack@5.103.0'),
    )
    assert.equal(result.ok, false)
    assert.deepEqual(result.staleEntries, ['webpack@4.47.0 || 5.102.1'])
  })

  it('skips wildcard pattern exclusions with a reason', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['@vercel/*'] },
      lock(),
    )
    assert.equal(result.ok, true)
    assert.equal(result.skippedEntries.length, 1)
    assert.equal(result.skippedEntries[0].entry, '@vercel/*')
    assert.match(result.skippedEntries[0].reason, /pattern/i)
  })

  it('skips bare package name exclusions (no @version) with a reason', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['react'] },
      lock(),
    )
    assert.equal(result.ok, true)
    assert.equal(result.skippedEntries.length, 1)
    assert.equal(result.skippedEntries[0].entry, 'react')
    assert.match(result.skippedEntries[0].reason, /version/i)
  })

  it('reports all stale entries, not just the first', () => {
    const result = checkExclusions(
      {
        minimumReleaseAgeExclude: [
          'next@16.2.6',
          '@next/env@16.2.6',
          'react@19.0.0',
        ],
      },
      lock('next@16.2.7', '@next/env@16.2.7', 'react@19.0.0'),
    )
    assert.equal(result.ok, false)
    assert.deepEqual(result.staleEntries, ['next@16.2.6', '@next/env@16.2.6'])
  })

  it('treats a scoped bare name (no @version) as bare, not stale', () => {
    const result = checkExclusions(
      { minimumReleaseAgeExclude: ['@scope/pkg'] },
      lock(),
    )
    assert.equal(result.ok, true)
    assert.equal(result.skippedEntries.length, 1)
    assert.match(result.skippedEntries[0].reason, /version/i)
  })
})
