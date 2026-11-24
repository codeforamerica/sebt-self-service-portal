// Locates a state's design token file: design/states/ (where Tokens Studio syncs), unless
// EXTERNAL_TOKENS_DIR_{STATE} points at a directory in the state's own asset repo.
import { existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const designSystemRoot = resolve(scriptDir, '../..')
const repoRoot = resolve(designSystemRoot, '../..')

/**
 * @param {string} state lowercase state code, e.g. 'co'
 * @param {Record<string, string|undefined>} env
 * @returns {{ path: string, label: string, external: boolean }} `label` names the
 *   source for generated-file headers without exposing a machine-specific path.
 */
export function resolveStateTokens(state, env = process.env) {
  const envVar = `EXTERNAL_TOKENS_DIR_${state.toUpperCase()}`
  const externalDir = env[envVar]

  if (!externalDir) {
    return {
      path: join(designSystemRoot, 'design', 'states', `${state}.json`),
      label: `design/states/${state}.json`,
      external: false
    }
  }

  const path = resolve(repoRoot, externalDir, `${state}.json`)
  // Never fall back to the in-repo copy: it may be stale for a state maintained elsewhere.
  if (!existsSync(path)) {
    throw new Error(`${envVar} is set, but ${state}.json was not found there: ${path}`)
  }
  return { path, label: `$${envVar}/${state}.json`, external: true }
}
