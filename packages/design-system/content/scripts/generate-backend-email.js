#!/usr/bin/env node
/**
 * Generate Backend Email Content Files from CSV
 *
 * Extracts OTP email content from the merged shared + state content and writes per-state JSON
 * files consumed by the .NET API as embedded resources.
 *
 * Usage (from src/SEBT.Portal.Web/):
 *   node ../../packages/design-system/content/scripts/generate-backend-email.js \
 *     --out-dir ../SEBT.Portal.Infrastructure/Templates/Email
 *
 * Output:
 *   EmailContent.dc.json  (embedded in SEBT.Portal.Infrastructure assembly)
 *   EmailContent.co.json
 *
 * CSV rows extracted (S8 - OTP Email Message):
 *   Sender  → programName
 *   Subject → subject
 *   Body 1  → body1
 *   Body 3  → body3
 *
 * States whose English "current" column is "!N/A!" are skipped entirely.
 * --csv-dir and --states point it at fixtures, as in generate-locales.js.
 */

import { mkdirSync, writeFileSync } from 'fs'
import { dirname, join, resolve } from 'path'
import { fileURLToPath } from 'url'

import { loadStateRows, locateContent, resolveStates } from './merge-content.js'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)

const cliArgs = process.argv.slice(2)
function getCliArg(name) {
  const idx = cliArgs.indexOf(name)
  return idx !== -1 ? cliArgs[idx + 1] : null
}

const outDirArg = getCliArg('--out-dir')
if (!outDirArg) {
  console.error('❌ --out-dir is required')
  process.exit(1)
}
const outDir = outDirArg.startsWith('/')
  ? outDirArg
  : join(process.cwd(), outDirArg)

const csvDirArg = getCliArg('--csv-dir')
const statesDir = csvDirArg ? resolve(process.cwd(), csvDirArg) : join(__dirname, '..', 'states')
const statesArg = getCliArg('--states')

const IGNORED = '!N/A!'

const ROW_KEYS = {
  'otp email message - sender':  'programName',
  'otp email message - subject': 'subject',
  'otp email message - body 1':  'body1',
  'otp email message - body 3':  'body3',
}

/** Build one state's email content from merged normalized rows ({ key, en, es, am }). */
function buildEmailContent(rows) {
  const locales = { en: {}, es: {}, am: {} }

  for (const row of rows) {
    const rawKey = row.key.trim().toLowerCase()
    const prop = Object.entries(ROW_KEYS).find(([k]) => rawKey.endsWith(k))?.[1]
    if (!prop) continue

    if (!row.en || row.en === IGNORED) return null  // state doesn't use email OTP

    locales.en[prop] = row.en
    if (row.es !== undefined) locales.es[prop] = row.es
    if (row.am !== undefined) locales.am[prop] = row.am
  }

  // Only emit locales that have at least a subject
  if (!locales.en.subject) return null

  const result = { en: locales.en }
  if (locales.es.subject) result.es = locales.es
  if (locales.am.subject) result.am = locales.am
  return result
}

function main() {
  console.log('📧 Generating backend email content files...')

  let content
  try {
    content = locateContent(statesDir, resolveStates(statesArg))
  } catch (error) {
    console.error(`❌ ${error.message}`)
    process.exit(1)
  }

  mkdirSync(outDir, { recursive: true })

  let written = 0
  for (const stateFile of content.states) {
    const { state } = stateFile
    const emailContent = buildEmailContent(loadStateRows(stateFile, content.sharedPath))

    if (!emailContent) {
      console.log(`   ${state.toUpperCase()}: skipped (no email OTP content)`)
      continue
    }

    const outPath = join(outDir, `EmailContent.${state}.json`)
    writeFileSync(outPath, JSON.stringify(emailContent, null, 2) + '\n', 'utf8')
    console.log(`   ${state.toUpperCase()}: wrote ${outPath}`)
    written++
  }

  console.log(`✅ Generated ${written} email content file(s)`)
}

main()
