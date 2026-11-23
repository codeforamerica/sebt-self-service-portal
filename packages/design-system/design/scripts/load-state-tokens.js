import { existsSync, readFileSync } from 'fs'
import { dirname, join, relative } from 'path'
import { fileURLToPath } from 'url'

const designDir = join(dirname(fileURLToPath(import.meta.url)), '..')

/** Token files that feed a state's theme: the shared base, then the state's overrides if it has any. */
export function tokenSourcePaths(state) {
  const statePath = join(designDir, 'states', `${state}.json`)
  return [join(designDir, 'shared.json'), ...(existsSync(statePath) ? [statePath] : [])]
}

/** The token files for generated-file headers, e.g. "design/shared.json + design/states/dc.json". */
export function tokenSourceLabel(state) {
  return tokenSourcePaths(state)
    .map((path) => relative(join(designDir, '..'), path))
    .join(' + ')
}

/**
 * Loads a state's design tokens: the shared base with the state's `theme` tokens layered on top,
 * like a later Tokens Studio set. Returns the shared file's shape with the merged `theme`.
 */
export function loadStateTokens(state) {
  const [sharedPath, statePath] = tokenSourcePaths(state)
  const shared = JSON.parse(readFileSync(sharedPath, 'utf8'))
  if (!statePath) {
    return shared
  }

  const overrides = JSON.parse(readFileSync(statePath, 'utf8')).theme
  return { ...shared, theme: { ...shared.theme, ...overrides } }
}
