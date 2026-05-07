#!/usr/bin/env node
/**
 * i18n Integrity Audit
 *
 * Cross-checks the source tree's `t('key')` call sites against the generated
 * locale JSON. Catches the failure modes that the CSV → JSON pipeline cannot:
 *
 *   1. used-but-missing : code calls a key the JSON doesn't have
 *   2. used-but-empty   : code calls a key the JSON has, but the value is ""
 *   3. orphan           : JSON ships a key nothing in code calls
 *   4. fallback-masking : both 1 or 2, but the call site has a string fallback,
 *                         so users see *something* — flagged as a warning,
 *                         not an error, since the fallback hides the gap
 *
 * Output is a punch list. Exit code is non-zero when there are unmasked
 * missing/empty hits, so the script can gate CI without false-failing on
 * day-one fallback noise.
 *
 * Usage:
 *   node audit-i18n.js \
 *     --src src/SEBT.Portal.Web/src \
 *     --locales src/SEBT.Portal.Web/content/locales
 *
 * Options:
 *   --src <dir>           Source tree to scan (recursive, .ts/.tsx)
 *   --locales <dir>       Root of generated locale JSON (locale/state/ns.json)
 *   --states <list>       Comma-separated state codes to enforce (default: all)
 *   --locales-list <l>    Comma-separated locale codes to enforce (default: all)
 *   --strict              Treat fallback-masked + orphan as errors too
 *   --json                Emit machine-readable JSON instead of a table
 *   --baseline <file>     Ignore errors recorded in this file (one-way ratchet)
 *   --update-baseline     Rewrite the baseline file from current errors
 */

import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'fs'
import { createRequire } from 'module'
import { dirname, join, relative, resolve } from 'path'
import { fileURLToPath } from 'url'

// Borrow the consumer workspace's TypeScript install (portal-web already has
// it as a devDependency). Keeping the audit script in design-system, where the
// generator lives, means we don't need to re-install typescript here.
const require = createRequire(`${process.cwd()}/`)
const ts = require('typescript')

const __dirname = dirname(fileURLToPath(import.meta.url))
const repoRoot = join(__dirname, '..', '..', '..', '..')

// ── CLI ────────────────────────────────────────────────────────────────────
const argv = process.argv.slice(2)
function arg(name, fallback) {
  const i = argv.indexOf(name)
  return i !== -1 ? argv[i + 1] : fallback
}
const flag = (name) => argv.includes(name)

const srcDir = arg('--src', join(repoRoot, 'src/SEBT.Portal.Web/src'))
const localesDir = arg('--locales', join(repoRoot, 'src/SEBT.Portal.Web/content/locales'))
const stateFilter = arg('--states', null)
const localeFilter = arg('--locales-list', null)
const strict = flag('--strict')
const asJson = flag('--json')
const baselinePath = arg('--baseline', null)
const updateBaseline = flag('--update-baseline')

// ── 1. Source scan: bindingName → namespace, then collect t(...) calls ────
function listFiles(dir) {
  const out = []
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === '.next') continue
    const full = join(dir, entry)
    const st = statSync(full)
    if (st.isDirectory()) out.push(...listFiles(full))
    else if (/\.(tsx?|jsx?)$/.test(entry) && !/\.(test|spec|stories)\./.test(entry))
      out.push(full)
  }
  return out
}

/** @typedef {{ file: string, line: number, ns: string, key: string, hasFallback: boolean }} CallSite */

function scanFile(filePath) {
  const text = readFileSync(filePath, 'utf8')
  const sf = ts.createSourceFile(filePath, text, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)

  /** binding name (e.g. "t", "tCommon") → namespace */
  const bindingToNs = new Map()
  /** @type {CallSite[]} */
  const calls = []

  function lineOf(node) {
    return sf.getLineAndCharacterOfPosition(node.getStart(sf)).line + 1
  }

  function visit(node) {
    // useTranslation('ns') destructured into a binding
    if (
      ts.isVariableDeclaration(node) &&
      node.initializer &&
      ts.isCallExpression(node.initializer) &&
      ts.isIdentifier(node.initializer.expression) &&
      node.initializer.expression.text === 'useTranslation' &&
      ts.isObjectBindingPattern(node.name)
    ) {
      const nsArg = node.initializer.arguments[0]
      let ns = null
      if (nsArg && ts.isStringLiteral(nsArg)) ns = nsArg.text
      // (multi-namespace array form is rare here; skip)
      if (ns) {
        for (const el of node.name.elements) {
          if (
            ts.isBindingElement(el) &&
            ts.isIdentifier(el.name) &&
            el.propertyName &&
            ts.isIdentifier(el.propertyName) &&
            el.propertyName.text === 't'
          ) {
            // const { t: tFoo } = useTranslation('ns')
            bindingToNs.set(el.name.text, ns)
          } else if (
            ts.isBindingElement(el) &&
            ts.isIdentifier(el.name) &&
            !el.propertyName &&
            el.name.text === 't'
          ) {
            // const { t } = useTranslation('ns')
            bindingToNs.set('t', ns)
          }
        }
      }
    }
    ts.forEachChild(node, visit)
  }
  visit(sf)

  // Pass 2: collect call sites for any binding name we know about.
  function visitCalls(node) {
    if (
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      bindingToNs.has(node.expression.text)
    ) {
      const ns = bindingToNs.get(node.expression.text)
      const keyArg = node.arguments[0]
      // Only static string keys are auditable. Dynamic keys (template literals
      // with substitution, identifiers, etc.) get skipped silently.
      if (keyArg && ts.isStringLiteral(keyArg)) {
        // Key may itself be "ns:bare" — split if so, otherwise inherit binding's ns.
        let resolvedNs = ns
        let key = keyArg.text
        if (key.includes(':')) {
          const idx = key.indexOf(':')
          resolvedNs = key.slice(0, idx)
          key = key.slice(idx + 1)
        }
        const second = node.arguments[1]
        const hasFallback = !!(second && ts.isStringLiteral(second))
        calls.push({
          file: relative(repoRoot, filePath),
          line: lineOf(node),
          ns: resolvedNs,
          key,
          hasFallback
        })
      }
    }
    ts.forEachChild(node, visitCalls)
  }
  visitCalls(sf)

  return calls
}

const allFiles = listFiles(srcDir)
/** @type {CallSite[]} */
const callSites = []
for (const f of allFiles) {
  try {
    callSites.push(...scanFile(f))
  } catch (err) {
    console.error(`Failed to parse ${f}: ${err.message}`)
  }
}

// ── 2. Load every JSON ────────────────────────────────────────────────────
function flatten(obj, prefix = '') {
  const out = {}
  for (const [k, v] of Object.entries(obj)) {
    const full = prefix ? `${prefix}.${k}` : k
    if (v && typeof v === 'object' && !Array.isArray(v)) Object.assign(out, flatten(v, full))
    else out[full] = v
  }
  return out
}

/** locale → state → ns:key → value */
const localesData = {}
const localesAvailable = new Set()
const statesAvailable = new Set()

for (const locale of readdirSync(localesDir)) {
  const localeDir = join(localesDir, locale)
  if (!statSync(localeDir).isDirectory()) continue
  localesAvailable.add(locale)
  localesData[locale] = {}
  for (const state of readdirSync(localeDir)) {
    const stateDir = join(localeDir, state)
    if (!statSync(stateDir).isDirectory()) continue
    statesAvailable.add(state)
    localesData[locale][state] = {}
    for (const file of readdirSync(stateDir)) {
      if (!file.endsWith('.json')) continue
      const ns = file.slice(0, -5)
      const json = JSON.parse(readFileSync(join(stateDir, file), 'utf8'))
      const flat = flatten(json)
      for (const [k, v] of Object.entries(flat)) {
        localesData[locale][state][`${ns}:${k}`] = v
      }
    }
  }
}

const enforcedLocales = localeFilter
  ? localeFilter.split(',').filter((l) => localesAvailable.has(l))
  : [...localesAvailable]
const enforcedStates = stateFilter
  ? stateFilter.split(',').filter((s) => statesAvailable.has(s))
  : [...statesAvailable]

// ── 3. Cross-check ────────────────────────────────────────────────────────
const issues = []

for (const call of callSites) {
  if (!call.ns) continue
  const compositeKey = `${call.ns}:${call.key}`
  for (const locale of enforcedLocales) {
    for (const state of enforcedStates) {
      const value = localesData[locale]?.[state]?.[compositeKey]
      const kind = value === undefined ? 'missing' : value === '' ? 'empty' : null
      if (!kind) continue
      issues.push({
        kind, // missing | empty
        masked: call.hasFallback,
        file: call.file,
        line: call.line,
        ns: call.ns,
        key: call.key,
        locale,
        state
      })
    }
  }
}

// Orphans: keys in JSON not referenced in code
const usedKeySet = new Set(callSites.map((c) => `${c.ns}:${c.key}`))
const orphans = []
for (const locale of enforcedLocales) {
  for (const state of enforcedStates) {
    for (const compositeKey of Object.keys(localesData[locale]?.[state] ?? {})) {
      if (!usedKeySet.has(compositeKey)) {
        orphans.push({ locale, state, ns: compositeKey.split(':')[0], key: compositeKey.split(':').slice(1).join(':') })
      }
    }
  }
}

// ── 4. Baseline (one-way ratchet) ─────────────────────────────────────────
// Items keyed by ns:key + kind + locale + state. Existing debt is grandfathered;
// any new issue that doesn't match the baseline still fails CI. Refresh with
// --update-baseline ONLY after a deliberate backfill — this is the lever the
// content team's punch-list work pulls down.
const issueId = (i) => `${i.kind}|${i.ns}:${i.key}|${i.locale}|${i.state}`

let baseline = new Set()
if (baselinePath && existsSync(baselinePath) && !updateBaseline) {
  const parsed = JSON.parse(readFileSync(baselinePath, 'utf8'))
  baseline = new Set((parsed.entries || []).map((e) => issueId(e)))
}

// ── 5. Output ─────────────────────────────────────────────────────────────
const errorsRaw = issues.filter((i) => !i.masked)
const errors = errorsRaw.filter((i) => !baseline.has(issueId(i)))
const errorsBaselined = errorsRaw.filter((i) => baseline.has(issueId(i)))
const masked = issues.filter((i) => i.masked)

if (updateBaseline && baselinePath) {
  // Snapshot every CURRENT error so a follow-up commit ratchets the floor.
  // Masked entries are tracked separately as warnings — they don't block CI
  // because the fallback string keeps the UI rendering, but they're real debt.
  const entries = errorsRaw
    .map((e) => ({ kind: e.kind, ns: e.ns, key: e.key, locale: e.locale, state: e.state }))
    .sort((a, b) => issueId(a).localeCompare(issueId(b)))
  writeFileSync(
    resolve(baselinePath),
    JSON.stringify(
      {
        $schema: 'i18n-audit-baseline-v1',
        recordedAt: new Date().toISOString().slice(0, 10),
        note: 'Grandfathered missing/empty translation issues. Shrink, never grow.',
        count: entries.length,
        entries
      },
      null,
      2
    ) + '\n'
  )
  console.log(`Updated baseline at ${baselinePath} with ${entries.length} entries.`)
  process.exit(0)
}

if (asJson) {
  console.log(
    JSON.stringify({ summary: { errors: errors.length, masked: masked.length, orphans: orphans.length }, errors, masked, orphans }, null, 2)
  )
} else {
  console.log(`\n=== i18n integrity audit ===`)
  console.log(`  call sites scanned : ${callSites.length}`)
  console.log(`  states enforced    : ${enforcedStates.join(', ') || '(none)'}`)
  console.log(`  locales enforced   : ${enforcedLocales.join(', ') || '(none)'}`)
  console.log(`  call→JSON errors   : ${errors.length} (no fallback, NOT in baseline)`)
  if (baseline.size > 0) {
    console.log(`  baseline entries   : ${baseline.size} (grandfathered, ${errorsBaselined.length} matched)`)
  }
  console.log(`  call→JSON masked   : ${masked.length} (fallback hides the gap)`)
  console.log(`  orphan JSON keys   : ${orphans.length}`)

  function group(rows) {
    const byKey = new Map()
    for (const r of rows) {
      const k = `${r.ns}:${r.key}`
      if (!byKey.has(k)) byKey.set(k, { ...r, locales: new Set(), states: new Set() })
      byKey.get(k).locales.add(r.locale)
      byKey.get(k).states.add(r.state)
    }
    return [...byKey.values()].sort((a, b) =>
      `${a.ns}:${a.key}`.localeCompare(`${b.ns}:${b.key}`)
    )
  }

  if (errors.length) {
    console.log(`\n— ERRORS (call sites with NO fallback hitting missing/empty JSON) —`)
    for (const e of group(errors).slice(0, 200)) {
      const where = `${[...e.locales].sort().join(',')} × ${[...e.states].sort().join(',')}`
      console.log(`  [${e.kind}] ${e.ns}:${e.key}  (${where})  ← ${e.file}:${e.line}`)
    }
    if (group(errors).length > 200) {
      console.log(`  …and ${group(errors).length - 200} more`)
    }
  }
  if (masked.length) {
    console.log(`\n— MASKED (fallback string in code hides the missing/empty value) —`)
    for (const e of group(masked).slice(0, 50)) {
      const where = `${[...e.locales].sort().join(',')} × ${[...e.states].sort().join(',')}`
      console.log(`  [${e.kind}] ${e.ns}:${e.key}  (${where})  ← ${e.file}:${e.line}`)
    }
    if (group(masked).length > 50) {
      console.log(`  …and ${group(masked).length - 50} more`)
    }
  }
  if (orphans.length) {
    const distinct = new Set(orphans.map((o) => `${o.ns}:${o.key}`))
    console.log(`\n— ORPHANS (${distinct.size} unique keys in JSON, never referenced in code) —`)
    for (const k of [...distinct].sort().slice(0, 50)) {
      console.log(`  ${k}`)
    }
    if (distinct.size > 50) console.log(`  …and ${distinct.size - 50} more`)
  }
}

const failed = errors.length > 0 || (strict && (masked.length > 0 || orphans.length > 0))
process.exit(failed ? 1 : 0)
