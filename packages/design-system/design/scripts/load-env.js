/**
 * Load .env.local for build scripts.
 *
 * Next.js loads .env.local automatically, but predev/prebuild scripts
 * are plain Node and run before Next.js starts. This module bridges
 * that gap so build scripts respect the same .env.local config.
 *
 * Import this at the top of any build script:
 *   import './load-env.js'           (from design/scripts/)
 *   import '../../design/scripts/load-env.js'  (from content/scripts/)
 *
 * Behavior:
 * - Reads .env.local from the project root (SEBT.Portal.Web/)
 * - Sets process.env for any key not already set
 * - Explicit env vars (e.g. STATE=co pnpm dev) take precedence
 * - Skips comments (#) and blank lines
 */
import { existsSync, readFileSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const rootDir = join(__dirname, '..', '..')
const envLocalPath = join(rootDir, '.env.local')

if (existsSync(envLocalPath)) {
  const content = readFileSync(envLocalPath, 'utf8')
  for (const line of content.split('\n')) {
    const trimmed = line.trim()
    if (!trimmed || trimmed.startsWith('#')) continue
    const eqIdx = trimmed.indexOf('=')
    if (eqIdx === -1) continue
    const key = trimmed.slice(0, eqIdx)
    const raw = trimmed.slice(eqIdx + 1)
    // Strip matching quotes — STATE="dc" and STATE='dc' should both yield dc
    const value = raw.replace(/^(['"])(.*)\1$/, '$2')
    // Don't override — explicit CLI env vars take precedence
    if (!process.env[key]) {
      process.env[key] = value
    }
  }
}
