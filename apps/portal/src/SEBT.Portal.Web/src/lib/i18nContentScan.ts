/**
 * Static reader for translation call sites and generated locale bundles.
 *
 * Test support only. It reads the repository from disk with `node:fs`, so it
 * must never be imported from application code.
 *
 * It exists because the failure this guards against is invisible to ordinary
 * component tests: those mock `useTranslation` with hand-written copy, so a key
 * that no longer exists in any bundle still resolves to whatever the mock
 * supplies. Comparing the keys the source asks for against the keys the content
 * pipeline actually produces is the only way to see the gap.
 */
import fs from 'node:fs'
import path from 'node:path'

/**
 * Repo root. Derived from the working directory rather than the module path so
 * it holds however Vitest transpiles this file: the runner's cwd is the web app
 * (where vitest.config.ts lives), which is four levels below the repo root.
 */
export const REPO_ROOT = path.resolve(process.cwd(), '../../../..')

export const APP_DIRS = {
  portal: path.join(REPO_ROOT, 'apps/portal/src/SEBT.Portal.Web'),
  checker: path.join(REPO_ROOT, 'apps/portal/src/SEBT.EnrollmentChecker.Web')
} as const

export type AppName = keyof typeof APP_DIRS

/**
 * The states each app actually ships. The enrollment checker builds only for
 * Colorado from this repo; the District of Columbia checker is a separate
 * application on a different stack. Its `dc` locale folders are generated
 * regardless, so they must not be treated as something this app renders.
 * Shipping a DC build from this app means adding it here and resolving whatever
 * the guards then report.
 */
export const APP_STATES: Record<AppName, string[]> = {
  portal: ['dc', 'co'],
  checker: ['co']
}

const DESIGN_SYSTEM_SRC = path.join(REPO_ROOT, 'packages/design-system/src')

/** Source trees whose `t()` calls can render inside a given app. */
export function sourceRoots(app: AppName): string[] {
  return [path.join(APP_DIRS[app], 'src'), DESIGN_SYSTEM_SRC]
}

export function walkSource(dir: string, out: string[] = []): string[] {
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      if (entry.name !== 'node_modules' && entry.name !== '.next') walkSource(full, out)
    } else if (/\.tsx?$/.test(entry.name) && !/\.(test|spec)\.tsx?$/.test(entry.name)) {
      out.push(full)
    }
  }
  return out
}

/** bundles[state][lang][namespace][key] = value */
export type Bundles = Record<string, Record<string, Record<string, Record<string, string>>>>

export function loadBundles(app: AppName): Bundles {
  const root = path.join(APP_DIRS[app], 'content/locales')
  const bundles: Bundles = {}
  for (const lang of fs.readdirSync(root)) {
    const langDir = path.join(root, lang)
    if (!fs.statSync(langDir).isDirectory()) continue
    for (const state of fs.readdirSync(langDir)) {
      const stateDir = path.join(langDir, state)
      if (!fs.statSync(stateDir).isDirectory()) continue
      for (const file of fs.readdirSync(stateDir)) {
        if (!file.endsWith('.json')) continue
        bundles[state] ??= {}
        bundles[state][lang] ??= {}
        bundles[state][lang][file.replace(/\.json$/, '')] = JSON.parse(
          fs.readFileSync(path.join(stateDir, file), 'utf8')
        ) as Record<string, string>
      }
    }
  }
  return bundles
}

export interface TranslationCall {
  /** Path relative to the repo root, for readable failure messages. */
  file: string
  line: number
  key: string
  /** Namespaces the alias could resolve to. `null` means it was computed at runtime. */
  namespaces: (string | null)[]
  /** `t('key', 'English fallback')` renders English rather than the key. */
  hasDefault: boolean
  /** Rendered inside a `<RichText>` wrapper, so markdown in the value becomes markup. */
  richWrapped: boolean
  snippet: string
}

// `const { t } = useTranslation('ns')` / `const { t: tCommon } = useTranslation('common')`
const HOOK_RE = /const\s*\{([^}]*)\}\s*=\s*useTranslation\(([^)]*)\)/g
// `t('key')`, `tCommon('key')`, `i18n.t('key')`, with or without a second argument.
const CALL_RE = /(?<![\w$])(i18n\.t|t|t[A-Z]\w*)\(\s*(['"])([^'"]*?)\2\s*(\)|,)/g

/**
 * Finds every literal translation key in a set of source files, resolving each
 * to the namespace(s) its alias was bound to. Template-literal keys are skipped:
 * only their prefix is knowable, and they cannot be checked for existence.
 */
export function extractTranslationCalls(files: string[]): TranslationCall[] {
  const calls: TranslationCall[] = []

  for (const file of files) {
    const text = fs.readFileSync(file, 'utf8')
    if (!/useTranslation|i18n\.t\(/.test(text)) continue
    const lines = text.split('\n')

    const aliasNs = new Map<string, Set<string | null>>()
    let hook: RegExpExecArray | null
    HOOK_RE.lastIndex = 0
    while ((hook = HOOK_RE.exec(text))) {
      const members = (hook[1] ?? '')
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean)
      const arg = (hook[2] ?? '').trim()
      const literal = arg.match(/^['"]([^'"]+)['"]$/)
      // No argument means the default namespace, which i18n.ts sets to `common`.
      const ns: string | null = arg === '' ? 'common' : (literal?.[1] ?? null)
      for (const member of members) {
        const alias = member === 't' ? 't' : member.match(/^t\s*:\s*(\w+)$/)?.[1]
        if (!alias) continue
        if (!aliasNs.has(alias)) aliasNs.set(alias, new Set())
        aliasNs.get(alias)!.add(ns)
      }
    }

    let call: RegExpExecArray | null
    CALL_RE.lastIndex = 0
    while ((call = CALL_RE.exec(text))) {
      const whole = call[0]
      const alias = call[1] ?? ''
      const rawKey = call[3] ?? ''
      const after = call[4] ?? ''
      if (!rawKey || rawKey.includes('${')) continue

      const lineNo = text.slice(0, call.index).split('\n').length
      const line = lines[lineNo - 1] ?? ''
      if (/^\s*(\/\/|\*|\/\*)/.test(line)) continue

      let namespaces: Set<string | null>
      let key = rawKey
      const qualified = rawKey.match(/^([\w-]+):(.+)$/)
      if (qualified?.[1] && qualified[2]) {
        namespaces = new Set<string | null>([qualified[1]])
        key = qualified[2]
      } else if (alias === 'i18n.t') {
        namespaces = new Set<string | null>(['common'])
      } else if (aliasNs.has(alias)) {
        namespaces = aliasNs.get(alias)!
      } else {
        // Some other function whose name starts with `t`; not a translation call.
        continue
      }

      const lookahead = text.slice(call.index + whole.length, call.index + whole.length + 300)
      // i18next's second positional argument is either a default value or an
      // options object. Anything that is not an object literal is a default,
      // including a variable or a nested `t()` call, so the key never renders
      // as itself. An object counts only when it carries `defaultValue`.
      const isOptionsObject = /^\s*\{/.test(lookahead)
      const hasDefault =
        after === ',' &&
        (!isOptionsObject || /^\s*\{[\s\S]{0,160}?\bdefaultValue\b/.test(lookahead))

      // `t('key', { ns: 'common' })` overrides the namespace the alias was bound
      // to. Missing this attributes the key to the wrong bundle and reports a
      // key that resolves perfectly well as absent.
      if (after === ',') {
        const nsOption = lookahead.match(/^\s*\{[^}]{0,160}?\bns:\s*['"]([\w-]+)['"]/)
        if (nsOption?.[1]) namespaces = new Set<string | null>([nsOption[1]])
      }

      // A RichText wrapper may sit on the same line or just above it.
      const context = [lines[lineNo - 3], lines[lineNo - 2], line].join(' ')

      calls.push({
        file: path.relative(REPO_ROOT, file),
        line: lineNo,
        key,
        namespaces: [...namespaces],
        hasDefault,
        richWrapped: /<RichText/.test(context),
        snippet: line.trim().slice(0, 100)
      })
    }
  }

  return calls
}

/**
 * A letter in any script is required so masked values such as `***-**-6789`
 * never match. `\p{L}` rather than `[A-Za-z]`: Amharic bold is bold too.
 */
const BOLD = /\*\*[^*\n]*\p{L}[^*\n]*\*\*/u
const LINK = /\[[^\]\n]+\]\([^)\n]+\)/

/** True when a locale value carries markdown that only `RichText` turns into markup. */
export function hasMarkdown(value: string): boolean {
  return BOLD.test(value) || LINK.test(value)
}

export interface StateExemption {
  /** `namespace:key`. */
  key: string
  /** States where the key is expected to be absent. */
  states: string[]
}

/**
 * Exemptions that no longer describe a gap. An entry is stale for an app when
 * its key is one the app's code asks for, yet none of the exempted states the
 * app ships still lacks it. Judged against every referenced key, not only the
 * unresolved ones: once exempted content returns, the key resolves everywhere
 * and would otherwise vanish from both sets and never be reported. Keys the
 * app never references belong to the other app and are not judged here.
 *
 * `live` holds `key|state` pairs that are still unresolved.
 */
export function staleExemptions<E extends StateExemption>(
  exempt: E[],
  referenced: ReadonlySet<string>,
  live: ReadonlySet<string>,
  appStates: readonly string[]
): E[] {
  return exempt.filter((e) => {
    const states = e.states.filter((s) => appStates.includes(s))
    if (states.length === 0 || !referenced.has(e.key)) return false
    return states.every((s) => !live.has(`${e.key}|${s}`))
  })
}

/** True when the bundle for this state and language defines the key in any of the call's namespaces. */
export function resolves(
  bundles: Bundles,
  state: string,
  lang: string,
  call: Pick<TranslationCall, 'key' | 'namespaces'>
): boolean {
  return call.namespaces.some(
    (ns) => ns !== null && bundles[state]?.[lang]?.[ns]?.[call.key] !== undefined
  )
}
