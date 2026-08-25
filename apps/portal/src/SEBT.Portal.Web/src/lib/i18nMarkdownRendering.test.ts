/**
 * Guard: copy that contains markdown is rendered through a component that understands it.
 *
 * Locale values may carry `**bold**` or `[text](url)`. Those only become markup
 * if the call site wraps the value in `<RichText>`; a bare `{t('key')}` inside a
 * `<p>` prints the asterisks and brackets as characters.
 *
 * This is the second half of what reached the Colorado sign-in page: the same
 * sync that dropped a key also added bold to a value whose call site rendered
 * plain text. As with the missing key, no code changed that day, so nothing in
 * review or in the component tests could have shown it.
 *
 * The check runs from the content side, find values that contain markdown, then
 * look at every place the code renders them, because that is the direction the
 * problem travels: content gains formatting, code stays as it was.
 */
import { describe, expect, it } from 'vitest'

import {
  extractTranslationCalls,
  loadBundles,
  sourceRoots,
  walkSource,
  type AppName,
  type TranslationCall
} from './i18nContentScan'

/** Mirrors i18nKeyCoverage.test.ts: the states each app actually ships. */
const APP_STATES: Record<AppName, string[]> = {
  portal: ['dc', 'co'],
  checker: ['co']
}

/** A letter is required so masked values such as `***-**-6789` never match. */
const BOLD = /\*\*[^*\n]*[A-Za-z][^*\n]*\*\*/
const LINK = /\[[^\]\n]+\]\([^)\n]+\)/

interface Exemption {
  /** `namespace:key`. */
  key: string
  reason: string
}

/**
 * Call sites that render markdown-bearing copy without a RichText wrapper on
 * purpose. Each entry asserts the asterisks or brackets are not visible to a
 * user, because the value is split or transformed before it reaches the DOM. If you cannot say that, wrap the call site instead.
 */
const EXEMPT: Exemption[] = []

interface Offender {
  id: string
  languages: string[]
  site: string
  snippet: string
}

/** Keys whose value carries markdown in at least one shipped state and language. */
function markdownKeys(app: AppName): Map<string, string[]> {
  const bundles = loadBundles(app)
  const keys = new Map<string, string[]>()
  for (const state of APP_STATES[app]) {
    const byLang = bundles[state] ?? {}
    for (const lang of Object.keys(byLang)) {
      for (const [ns, entries] of Object.entries(byLang[lang] ?? {})) {
        for (const [key, value] of Object.entries(entries)) {
          if (typeof value !== 'string') continue
          if (!BOLD.test(value) && !LINK.test(value)) continue
          const id = `${ns}:${key}`
          const seen = keys.get(id) ?? []
          const where = `${state}/${lang}`
          if (!seen.includes(where)) seen.push(where)
          keys.set(id, seen)
        }
      }
    }
  }
  return keys
}

function unwrappedCallSites(app: AppName): Offender[] {
  const calls = extractTranslationCalls(sourceRoots(app).flatMap((root) => walkSource(root)))
  const offenders: Offender[] = []

  const rendersKey = (call: TranslationCall, ns: string, key: string) =>
    call.key === key && call.namespaces.includes(ns)

  for (const [id, languages] of markdownKeys(app)) {
    const split = id.indexOf(':')
    const ns = id.slice(0, split)
    const key = id.slice(split + 1)
    if (EXEMPT.some((e) => e.key === id)) continue

    for (const call of calls) {
      if (!rendersKey(call, ns, key)) continue
      if (call.richWrapped) continue
      offenders.push({
        id,
        languages,
        site: `${call.file}:${call.line}`,
        snippet: call.snippet
      })
    }
  }

  return offenders
}

describe.each(Object.keys(APP_STATES) as AppName[])(
  '%s: markdown in copy renders as markup',
  (app) => {
    it('has no markdown-bearing key rendered as plain text', () => {
      const offenders = unwrappedCallSites(app)
      const detail = offenders
        .map((o) => `  ${o.id} (${o.languages.join(', ')})\n      ${o.site}\n      ${o.snippet}`)
        .join('\n')

      expect(
        offenders,
        offenders.length
          ? `\n\nThese locale values contain markdown, but the code renders them as plain\n` +
              `text, so a reader sees the asterisks or the bracket syntax:\n\n${detail}\n\n` +
              `Wrap the value in <RichText> (use <RichText inline> inside a sentence), or,\n` +
              `if the markup is stripped before it reaches the DOM, add an entry to EXEMPT\n` +
              `in this file explaining why.\n`
          : undefined
      ).toEqual([])
    })

    it('has no stale exemption', () => {
      const live = new Set(markdownKeys(app).keys())
      const stale = EXEMPT.filter((e) => !live.has(e.key))
      expect(stale.map((e) => e.key)).toEqual([])
    })
  }
)
