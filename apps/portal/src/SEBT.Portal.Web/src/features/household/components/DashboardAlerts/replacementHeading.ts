/**
 * Fills the `dashboard.alertAddressTitle` template with the cards that were
 * just replaced.
 *
 * The template carries two bracket conventions from the content sheet:
 * - a double-bracketed example list, e.g. `[[9999], [9999], and [9999],]` or
 *   `[[First name] [Last name], and [First name] [Last name]]`, replaced
 *   wholesale with a locale-formatted list of the real values;
 * - single-bracket plural markers outside the list, e.g. `card[s]` (Amharic
 *   `ካርድ[ዎች]`), whose inner text is kept only for more than one item.
 *
 * Returns null when there is nothing to list or the template's brackets don't
 * parse (some translations currently ship malformed brackets), so callers can
 * omit the heading rather than render raw tokens.
 */
export function fillReplacementHeading(
  template: string,
  items: string[],
  language: string
): string | null {
  if (items.length === 0) {
    return null
  }

  const start = template.indexOf('[[')
  if (start === -1) {
    return null
  }

  let depth = 0
  let end = -1
  for (let i = start; i < template.length; i++) {
    const char = template.charAt(i)
    if (char === '[') {
      depth++
    } else if (char === ']') {
      depth--
      if (depth === 0) {
        end = i
        break
      }
    }
  }
  if (end === -1) {
    return null
  }

  const prefix = fillPluralTokens(template.slice(0, start), items.length)
  const suffix = fillPluralTokens(template.slice(end + 1), items.length)
  if (prefix === null || suffix === null) {
    return null
  }

  return prefix + formatList(items, language) + suffix
}

function fillPluralTokens(segment: string, count: number): string | null {
  const filled = segment.replace(/\[([^[\]]*)\]/g, (_, inner: string) => (count > 1 ? inner : ''))
  return filled.includes('[') || filled.includes(']') ? null : filled
}

function formatList(items: string[], language: string): string {
  try {
    return new Intl.ListFormat(language, { style: 'long', type: 'conjunction' }).format(items)
  } catch {
    return new Intl.ListFormat('en', { style: 'long', type: 'conjunction' }).format(items)
  }
}
