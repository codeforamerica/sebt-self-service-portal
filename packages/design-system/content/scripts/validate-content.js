/**
 * Content checks for generated locale data.
 *
 * A change to the content sheet can break what a user sees without touching
 * any code: a lost `{` turns `{{count}}` into literal text; a translation that
 * loses one `]` makes a runtime template filler give up and the heading
 * disappears; a lost `**` prints literal asterisks. These checks read only the
 * locale values, so they run for every state and both web apps from
 * generate-locales.js. They have no I/O.
 *
 * Every rule here changes what a user sees, so a finding is an error, whether
 * or not any code renders the key today: the defect is in the sheet either way.
 */

// Marks a list the code fills at runtime, e.g. "[[9999], [9999], and [9999],]".
const EXAMPLE_LIST = '[['

// A plural marker such as "card[s]": brackets around text that holds no brackets.
const PLURAL_MARKER = /\[[^[\]]*\]/g

const PLACEHOLDER = /\{\{[^{}]*\}\}|\{[^{}]*\}/g

const PARAGRAPH_BREAK = /\n\s*\n/

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

/** Index of the bracket that closes the one at `start`, or -1 when none does. */
function closingBracket(value, start) {
  let depth = 0
  for (let i = start; i < value.length; i++) {
    if (value[i] === '[') {
      depth++
    } else if (value[i] === ']') {
      depth--
      if (depth === 0) {
        return i
      }
    }
  }
  return -1
}

/**
 * Explains why the runtime filler cannot fill a template that carries an example
 * list, or null when it can. Balanced brackets are not enough: the filler
 * (fillReplacementHeading in the portal's replacementHeading.ts) replaces the
 * list wholesale, then accepts only plain `[text]` plural markers before and
 * after it. Anything else makes it return nothing, and the heading disappears.
 * This follows the same steps, so a change to the filler's rules belongs here too.
 */
export function unfillableTemplate(value) {
  const start = value.indexOf(EXAMPLE_LIST)
  if (start === -1) {
    return null
  }

  const end = closingBracket(value, start)
  if (end === -1) {
    return 'the example list never closes'
  }

  const outsideTheList = [value.slice(0, start), value.slice(end + 1)]
  const hasStrayBracket = outsideTheList.some((segment) => /[[\]]/.test(segment.replace(PLURAL_MARKER, '')))
  return hasStrayBracket ? 'a bracket outside the example list is not a plain "[text]" marker' : null
}

/**
 * True when some paragraph holds an odd number of `**`, so a bold span never
 * closes. Counted per paragraph because a pair split across a blank line never
 * renders as bold.
 */
export function hasOddBoldMarkers(value) {
  return value
    .split(PARAGRAPH_BREAK)
    .some((paragraph) => (paragraph.match(/\*\*/g) ?? []).length % 2 === 1)
}

/** True when a `{` or `}` is not part of a whole `{name}` or `{{name}}` placeholder. */
export function hasBraceMismatch(value) {
  return /[{}]/.test(value.replace(PLACEHOLDER, ''))
}

/**
 * Check one state's locale data.
 *
 * @param stateData  { locale: { namespace: { key: value } } }
 * @param state      state code, used to label findings
 * @returns { errors } of { rule, state, locale, key, detail, value }
 */
export function validateStateContent(stateData, state) {
  const errors = []
  const english = stateData.en ?? {}

  function report(rule, locale, key, detail, value) {
    errors.push({ rule, state, locale, key, detail, value })
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
            report('unbalanced-brackets', locale, key, imbalance, value)
          } else if (source.includes(EXAMPLE_LIST)) {
            if (!value.includes(EXAMPLE_LIST)) {
              report(
                'missing-example-list',
                locale,
                key,
                `English has a "${EXAMPLE_LIST} ... ]" list that this value does not`,
                value
              )
            } else {
              const unfillable = unfillableTemplate(value)
              if (unfillable) {
                report('unfillable-template', locale, key, unfillable, value)
              }
            }
          }
        }

        if (hasOddBoldMarkers(value)) {
          report('odd-bold-markers', locale, key, 'a "**" marker has no partner in its paragraph', value)
        }

        if (hasBraceMismatch(value)) {
          report(
            'mismatched-braces',
            locale,
            key,
            'a "{" or "}" is not part of a whole {placeholder}',
            value
          )
        }
      }
    }
  }

  return { errors }
}
