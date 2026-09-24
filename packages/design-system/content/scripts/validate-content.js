/**
 * Content checks for generated locale data.
 *
 * A change to the content sheet can break what a user sees without touching
 * any code: a lost `{` turns `{{count}}` into literal text; a translation that
 * loses one `]` makes a runtime template filler give up and the heading
 * disappears; a lost `**` prints literal asterisks. These checks read only the
 * locale values, so they run for every state and both web apps from
 * generate-locales.js. They have no I/O. The one thing they learn about app
 * source arrives as `isReferenced` (see referenced-keys.js).
 *
 * Every rule here changes what a user sees, so a finding is an error. A defect
 * in a key that no code renders cannot reach a user today, so it is reported
 * as a warning instead.
 */

// Marks a list the code fills at runtime, e.g. "[[9999], [9999], and [9999],]".
const EXAMPLE_LIST = '[['

/** Explains why a value's square brackets do not balance, or null when they do. */
export function bracketImbalance(value) {
  let depth = 0
  for (const char of value) {
    if (char === '[') {
      depth++
    } else if (char === ']') {
      depth--
      if (depth < 0) {
        return 'closes a bracket that was never opened'
      }
    }
  }
  return depth > 0 ? `leaves ${depth} bracket(s) unclosed` : null
}

/** True when `**` appears an odd number of times, so one bold span never closes. */
export function hasOddBoldMarkers(value) {
  return (value.match(/\*\*/g) ?? []).length % 2 === 1
}

/** True when `{` and `}` counts differ, so an interpolation placeholder is broken. */
export function hasBraceMismatch(value) {
  return (value.match(/\{/g) ?? []).length !== (value.match(/\}/g) ?? []).length
}

/**
 * Check one state's locale data.
 *
 * @param stateData     { locale: { namespace: { key: value } } }
 * @param state         state code, used to label findings
 * @param isReferenced  optional (namespace, name) => boolean. When given, a
 *                      defect in a key it rejects is a warning, not an error.
 *                      When absent, every key is treated as rendered.
 * @returns { errors, warnings } of { rule, state, locale, key, detail, value }
 */
export function validateStateContent(stateData, state, { isReferenced }) {
  const errors = []
  const warnings = []
  const english = stateData.en ?? {}

  function finding(rule, locale, key, detail, value) {
    return { rule, state, locale, key, detail, value }
  }

  function reportDefect(namespace, name, found) {
    if (isReferenced && !isReferenced(namespace, name)) {
      warnings.push({
        ...found,
        detail: `${found.detail}; no app code references this key, so nothing renders it today`
      })
    } else {
      errors.push(found)
    }
  }

  for (const [locale, namespaces] of Object.entries(stateData)) {
    for (const [namespace, entries] of Object.entries(namespaces ?? {})) {
      for (const [name, value] of Object.entries(entries)) {
        if (typeof value !== 'string' || value === '') continue

        const key = `${namespace}.${name}`
        const source = english[namespace]?.[name] ?? ''

        // English is the anchor: a stray bracket in plain prose is visible on the
        // page, but a broken bracket in template copy silently removes content.
        if (/[[\]]/.test(source)) {
          const imbalance = bracketImbalance(value)
          if (imbalance) {
            reportDefect(namespace, name, finding('unbalanced-brackets', locale, key, imbalance, value))
          } else if (source.includes(EXAMPLE_LIST) && !value.includes(EXAMPLE_LIST)) {
            reportDefect(
              namespace,
              name,
              finding(
                'missing-example-list',
                locale,
                key,
                `English has a "${EXAMPLE_LIST} ... ]" list that this value does not`,
                value
              )
            )
          }
        }

        if (hasOddBoldMarkers(value)) {
          reportDefect(
            namespace,
            name,
            finding('odd-bold-markers', locale, key, 'a "**" marker has no partner', value)
          )
        }

        if (hasBraceMismatch(value)) {
          reportDefect(
            namespace,
            name,
            finding('mismatched-braces', locale, key, '"{" and "}" counts differ', value)
          )
        }
      }
    }
  }

  return { errors, warnings }
}
