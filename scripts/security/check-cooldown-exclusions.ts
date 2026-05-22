#!/usr/bin/env node
// Verifies that every version-pinned entry in `minimumReleaseAgeExclude`
// (pnpm-workspace.yaml) still resolves to the exempted version in pnpm-lock.yaml.
// When the lockfile moves past an exempted version, the entry is stale and the
// cooldown defense it bypasses can be safely re-engaged by removing the entry.

import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse as parseYaml } from 'yaml'

export interface WorkspaceConfig {
  minimumReleaseAgeExclude?: string[]
}

export interface SkippedEntry {
  entry: string
  reason: string
}

export interface CheckResult {
  ok: boolean
  staleEntries: string[]
  skippedEntries: SkippedEntry[]
}

export function checkExclusions(
  workspace: WorkspaceConfig,
  lockfilePackages: Set<string>,
): CheckResult {
  const entries = workspace.minimumReleaseAgeExclude ?? []
  const stale: string[] = []
  const skipped: SkippedEntry[] = []

  for (const entry of entries) {
    if (entry.includes('*')) {
      skipped.push({
        entry,
        reason: 'pattern exclusion — patterns do not age out by version',
      })
      continue
    }

    // Scoped packages start with `@` (e.g. `@next/env`). The delimiter between
    // name and version is the *next* `@`, so start the search at index 1.
    const delimiter = entry.indexOf('@', 1)
    const versionSpec = delimiter === -1 ? '' : entry.slice(delimiter + 1).trim()
    if (!versionSpec) {
      skipped.push({
        entry,
        reason: 'bare package name — no @version pin to verify',
      })
      continue
    }

    const name = entry.slice(0, delimiter)
    const versions = versionSpec
      .split('||')
      .map((v) => v.trim())
      .filter(Boolean)

    const anyResolved = versions.some((v) => lockfilePackages.has(`${name}@${v}`))
    if (!anyResolved) {
      stale.push(entry)
    }
  }

  return {
    ok: stale.length === 0,
    staleEntries: stale,
    skippedEntries: skipped,
  }
}

async function main(): Promise<void> {
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..')
  const workspacePath = resolve(repoRoot, 'pnpm-workspace.yaml')
  const lockfilePath = resolve(repoRoot, 'pnpm-lock.yaml')

  const [workspaceRaw, lockfileRaw] = await Promise.all([
    readFile(workspacePath, 'utf8'),
    readFile(lockfilePath, 'utf8'),
  ])

  const workspace = (parseYaml(workspaceRaw) ?? {}) as WorkspaceConfig
  const lockfile = (parseYaml(lockfileRaw) ?? {}) as { packages?: Record<string, unknown> }
  const lockfilePackages = new Set(Object.keys(lockfile.packages ?? {}))

  const result = checkExclusions(workspace, lockfilePackages)

  for (const skipped of result.skippedEntries) {
    console.warn(`⚠️  ${skipped.entry}: ${skipped.reason}`)
  }

  if (!result.ok) {
    console.error('')
    console.error('❌ Stale minimumReleaseAgeExclude entries detected in pnpm-workspace.yaml:')
    console.error('')
    for (const entry of result.staleEntries) {
      console.error(`   - ${entry}`)
    }
    console.error('')
    console.error('The lockfile no longer resolves to these exact versions, so the cooldown')
    console.error('exemptions are doing nothing. Remove them from pnpm-workspace.yaml to')
    console.error('re-engage the minimumReleaseAge defense for these packages.')
    process.exit(1)
  }

  const checkedCount =
    (workspace.minimumReleaseAgeExclude?.length ?? 0) - result.skippedEntries.length
  console.log(`✅ ${checkedCount} version-pinned cooldown exclusion(s) still match the lockfile.`)
}

const invokedDirectly = process.argv[1] === fileURLToPath(import.meta.url)
if (invokedDirectly) {
  main().catch((err) => {
    console.error(err)
    process.exit(1)
  })
}
