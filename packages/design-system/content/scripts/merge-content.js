// Reads content/shared.csv and content/states/{state}.csv into { key, en, es, am } rows, where a
// language is undefined when its CSV has no column for it, and layers a state's rows over shared.

import { existsSync, readFileSync } from 'fs'
import { dirname, join } from 'path'

import { STATE_CODES } from '../../src/lib/state.ts'

/**
 * Parse CSV content into rows
 * Handles quoted fields with commas and newlines
 */
export function parseCSV(content) {
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
        if (currentRow.some((f) => f)) {
          rows.push(currentRow)
        }
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
    if (currentRow.some((f) => f)) {
      rows.push(currentRow)
    }
  }

  return rows
}

/**
 * Find the column indices in a CSV header row. Only "current" language columns are
 * read (e.g. "🟡 DC English Current", not "⚪ SOURCE English"); the key column is
 * "Variable Name/Key" or any header containing "content".
 */
function detectColumns(headerRow) {
  const find = (needle) => headerRow.findIndex((h) => h.toLowerCase().includes(needle))
  return {
    key: headerRow.findIndex((h) => {
      const lower = h.toLowerCase()
      return lower.includes('content') || lower.includes('variable name')
    }),
    en: find('english current'),
    es: find('español current'),
    am: find('amharic current'),
  }
}

/** Normalize parsed CSV rows (header first) into { key, en, es, am } rows. */
export function normalizeRows(csvRows, label) {
  const [headerRow, ...dataRows] = csvRows
  const cols = detectColumns(headerRow ?? [])

  if (cols.key === -1 || cols.en === -1) {
    throw new Error(`CSV for ${label} must have "Content" and "English" columns`)
  }

  const cell = (row, idx) => (idx === -1 ? undefined : row[idx] || '')

  return dataRows
    .filter((row) => row[cols.key] && row[cols.key].trim())
    .map((row) => ({
      key: row[cols.key],
      en: cell(row, cols.en),
      es: cell(row, cols.es),
      am: cell(row, cols.am),
    }))
}

/** Read a CSV file into normalized rows. */
function readContentRows(csvPath, label) {
  return normalizeRows(parseCSV(readFileSync(csvPath, 'utf8')), label)
}

const normalizedKey = (row) => row.key.toLowerCase().replace(/\s+/g, ' ').trim()

/**
 * Merge shared rows with a state's rows. A state row replaces every shared row with the same
 * key, even when its value is blank or the ignore marker; `keyOf` decides when keys match.
 */
export function mergeContentRows(sharedRows, stateRows, keyOf = normalizedKey) {
  const stateKeys = new Set(stateRows.map(keyOf))
  return [...sharedRows.filter((row) => !stateKeys.has(keyOf(row))), ...stateRows]
}

/** shared.csv sits beside the states directory, never inside it. */
export function sharedCsvPathFor(statesDir) {
  return join(dirname(statesDir), 'shared.csv')
}

/** Comma-separated override (for fixtures) or every registered state code. */
export function resolveStates(statesArg) {
  if (!statesArg) return [...STATE_CODES]
  return statesArg.split(',').map((s) => s.trim().toLowerCase()).filter(Boolean)
}

/**
 * Locate the content sources for each state. A state needs its own CSV, shared.csv, or both.
 * Returns { sharedPath: string | null, states: [{ state, csvPath: string | null }] }.
 */
export function locateContent(statesDir, states) {
  const sharedPath = sharedCsvPathFor(statesDir)
  const hasShared = existsSync(sharedPath)

  const located = states.map((state) => {
    const csvPath = join(statesDir, `${state}.csv`)
    const hasCsv = existsSync(csvPath)
    if (!hasCsv && !hasShared) {
      throw new Error(
        `No content for state "${state}": expected ${csvPath} or a shared content file at ${sharedPath}`
      )
    }
    return { state, csvPath: hasCsv ? csvPath : null }
  })

  return { sharedPath: hasShared ? sharedPath : null, states: located }
}

/** Merged normalized rows for one state. */
export function loadStateRows({ state, csvPath }, sharedPath, keyOf) {
  const sharedRows = sharedPath ? readContentRows(sharedPath, 'shared content') : []
  const stateRows = csvPath ? readContentRows(csvPath, state) : []
  return mergeContentRows(sharedRows, stateRows, keyOf)
}
