/**
 * Which locale keys does an app's source reference?
 *
 * The content checks fail a run only for copy a user can see. A defect in a key
 * that no code renders is still worth fixing in the sheet, but it must not block
 * a pull request. This module answers "could this key render?" from source text
 * alone. It has no I/O: the caller reads the files.
 *
 * The answer leans towards "yes". A key is unreferenced only when nothing points
 * at it:
 * - `t('key')`, or an alias such as `tDev('key')`, references the key in every
 *   namespace the file passes to `useTranslation('namespace')`.
 * - `t('namespace:key')` references that namespace only.
 * - A call whose key is built at runtime, `t(field.labelKey)` or a template
 *   literal with `${...}`, could name any key, so every key in the file's
 *   namespaces counts as referenced.
 * - A file that names no namespace (it receives `t` as a prop, or passes a
 *   variable to `useTranslation`) references its literal keys in every namespace,
 *   and a runtime-built key there opens the default namespace.
 *
 * Comments are not stripped, so a call shape quoted in a comment counts too.
 * That errs on the same side.
 */

export const DEFAULT_NAMESPACE = 'common'

const NAMESPACE_IN_USE = /useTranslation\(\s*(?:'([^']+)'|"([^"]+)")/g

// `t(` or an alias such as `tDev(`, with its first argument: a quoted literal, a
// template literal, or the first character of anything else. The lookbehind keeps
// `format(`, `parseInt(`, and `it(` out.
const TRANSLATE_CALL = /(?<![\w$])t(?:[A-Z]\w*)?\(\s*(?:'([^']*)'|"([^"]*)"|`([^`]*)`|([^\s)]))/g

/**
 * @param files  [{ path, text }] source files of one app and the packages it renders
 * @returns { keys: Set<'namespace.key'>, anyNamespaceKeys: Set<'key'>, openNamespaces: Set<'namespace'> }
 */
export function collectReferences(files) {
  const keys = new Set()
  const anyNamespaceKeys = new Set()
  const openNamespaces = new Set()

  for (const { text } of files) {
    const namespaces = [...text.matchAll(NAMESPACE_IN_USE)].map((m) => m[1] ?? m[2])

    for (const [, single, double, template, other] of text.matchAll(TRANSLATE_CALL)) {
      const builtAtRuntime = other !== undefined || template?.includes('${')
      if (builtAtRuntime) {
        const opened = namespaces.length > 0 ? namespaces : [DEFAULT_NAMESPACE]
        opened.forEach((namespace) => openNamespaces.add(namespace))
        continue
      }

      const literal = single ?? double ?? template
      const separator = literal.indexOf(':')
      if (separator !== -1) {
        keys.add(`${literal.slice(0, separator)}.${literal.slice(separator + 1)}`)
      } else if (namespaces.length > 0) {
        namespaces.forEach((namespace) => keys.add(`${namespace}.${literal}`))
      } else {
        anyNamespaceKeys.add(literal)
      }
    }
  }

  return { keys, anyNamespaceKeys, openNamespaces }
}

/** @returns (namespace, name) => true when some code could render that key */
export function createIsReferenced({ keys, anyNamespaceKeys, openNamespaces }) {
  return (namespace, name) =>
    openNamespaces.has(namespace) || keys.has(`${namespace}.${name}`) || anyNamespaceKeys.has(name)
}
