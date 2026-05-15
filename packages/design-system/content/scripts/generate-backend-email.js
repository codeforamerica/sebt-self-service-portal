#!/usr/bin/env node
/**
 * Generate Backend Email Content Files from CSV
 *
 * Extracts OTP email content from state CSV files and writes per-state JSON
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
 */

import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'

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

const statesDir = join(__dirname, '..', 'states')

const IGNORED = '!N/A!'

const ROW_KEYS = {
  'otp email message - sender':  'programName',
  'otp email message - subject': 'subject',
  'otp email message - body 1':  'body1',
  'otp email message - body 3':  'body3',
}

/**
 * Parse CSV content into rows (handles quoted fields with embedded commas/newlines).
 */
function parseCSV(content) {
  const rows = []
  let currentRow = []
  let currentField = ''
  let inQuotes = false

  for (let i = 0; i < content.length; i++) {
    const char = content[i]
    const nextChar = content[i + 1]

    if (inQuotes) {
      if (char === '"' && nextChar === '"') {
        currentField += '"'
        i++
      } else if (char === '"') {
        inQuotes = false
      } else {
        currentField += char
      }
    } else {
      if (char === '"') {
        inQuotes = true
      } else if (char === ',') {
        currentRow.push(currentField.trim())
        currentField = ''
      } else if (char === '\n' || (char === '\r' && nextChar === '\n')) {
        currentRow.push(currentField.trim())
        if (currentRow.some((f) => f)) rows.push(currentRow)
        currentRow = []
        currentField = ''
        if (char === '\r') i++
      } else if (char !== '\r') {
        currentField += char
      }
    }
  }

  if (currentField || currentRow.length > 0) {
    currentRow.push(currentField.trim())
    if (currentRow.some((f) => f)) rows.push(currentRow)
  }

  return rows
}

function buildEmailContent(rows) {
  const [headerRow, ...dataRows] = rows

  const contentIdx = headerRow.findIndex((h) => {
    const lower = h.toLowerCase()
    return lower.includes('content') || lower.includes('variable name')
  })
  const englishIdx = headerRow.findIndex((h) => h.toLowerCase().includes('english current'))
  const spanishIdx = headerRow.findIndex((h) => h.toLowerCase().includes('español current'))
  const amharicIdx = headerRow.findIndex((h) => h.toLowerCase().includes('amharic current'))

  if (contentIdx === -1 || englishIdx === -1) return null

  const locales = { en: {}, es: {}, am: {} }

  for (const row of dataRows) {
    const rawKey = (row[contentIdx] || '').trim().toLowerCase()
    const prop = Object.entries(ROW_KEYS).find(([k]) => rawKey.endsWith(k))?.[1]
    if (!prop) continue

    const en = row[englishIdx] || ''
    if (!en || en === IGNORED) return null  // state doesn't use email OTP

    locales.en[prop] = en
    if (spanishIdx !== -1) locales.es[prop] = row[spanishIdx] || ''
    if (amharicIdx !== -1) locales.am[prop] = row[amharicIdx] || ''
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

  if (!existsSync(statesDir)) {
    console.warn('⚠️  No states directory found at', statesDir)
    process.exit(0)
  }

  const csvFiles = readdirSync(statesDir)
    .filter((f) => f.endsWith('.csv'))
    .map((f) => ({ state: f.replace('.csv', '').toLowerCase(), csvPath: join(statesDir, f) }))

  if (csvFiles.length === 0) {
    console.warn('⚠️  No state CSV files found')
    process.exit(0)
  }

  mkdirSync(outDir, { recursive: true })

  let written = 0
  for (const { state, csvPath } of csvFiles) {
    const rows = parseCSV(readFileSync(csvPath, 'utf8'))
    const content = buildEmailContent(rows)

    if (!content) {
      console.log(`   ${state.toUpperCase()}: skipped (no email OTP content)`)
      continue
    }

    const outPath = join(outDir, `EmailContent.${state}.json`)
    const header = '// Auto-generated by content/scripts/generate-backend-email.js — DO NOT EDIT\n'
    writeFileSync(outPath, header + JSON.stringify(content, null, 2) + '\n', 'utf8')
    console.log(`   ${state.toUpperCase()}: wrote ${outPath}`)
    written++
  }

  console.log(`✅ Generated ${written} email content file(s)`)
}

main()
