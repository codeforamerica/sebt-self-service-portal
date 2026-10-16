/** Roman-numeral suffixes kept uppercase, so "SMITH III" doesn't become "Smith Iii". */
const GENERATIONAL_SUFFIXES = new Set([
  "ii",
  "iii",
  "iv",
  "v",
  "vi",
  "vii",
  "viii",
  "ix",
  "x",
]);

/**
 * Captures a name part's boundary and first letter, e.g. "Mary-Jane", "O'Brien", "J.R.".
 * Whitespace isn't a boundary here because the caller splits on it first.
 */
const NAME_PART_FIRST_LETTER = /(^|[-.'’])(\p{L})/gu;

const NON_LETTERS = /[^\p{L}]/gu;

/**
 * Whether the letters are all one case, meaning the casing carries no intent.
 * Non-letters are ignored; fewer than two letters counts as uniform.
 *
 * @param value - The full name to inspect.
 * @returns True if every letter shares one case, false if the name is mixed case.
 *
 * @example
 * hasUniformCase("O'BRIEN")   // true
 * hasUniformCase('della')     // true
 * hasUniformCase('MacDonald') // false
 */
function hasUniformCase(value: string): boolean {
  const letters = value.replace(NON_LETTERS, "");
  if (letters.length < 2) {
    return true;
  }
  return letters === letters.toLowerCase() || letters === letters.toUpperCase();
}

/**
 * Lower-cases a word, then capitalizes the first letter of each name part.
 *
 * @param word - A single whitespace-free word.
 * @returns The word with each hyphen-, period-, or apostrophe-separated part capitalized.
 *
 * @example
 * titleCaseWord('MARY-JANE') // 'Mary-Jane'
 * titleCaseWord("O'BRIEN")   // "O'Brien"
 */
function titleCaseWord(word: string): string {
  return word
    .toLowerCase()
    .replace(
      NAME_PART_FIRST_LETTER,
      (_match, boundary: string, letter: string) =>
        boundary + letter.toUpperCase(),
    );
}

/**
 * Cases one word as either a generational suffix or an ordinary name part.
 *
 * @param word - A single whitespace-free word.
 * @param isTrailing - True only for the last word of a multi-word name. Restricting
 *   suffixes to that position keeps given names like "Vi" or "Iv" from becoming "VI"/"IV".
 * @returns The word uppercased if it's a trailing Roman-numeral suffix, else title-cased.
 *
 * @example
 * formatNameWord('III', true) // 'III'
 * formatNameWord('VI', false) // 'Vi'
 * formatNameWord('JR', true)  // 'Jr'
 */
function formatNameWord(word: string, isTrailing: boolean): string {
  if (isTrailing) {
    const letters = word.replace(NON_LETTERS, "").toLowerCase();

    // "Jr"/"Sr" fall through to title casing rather than shouting.
    if (GENERATIONAL_SUFFIXES.has(letters)) {
      return word.toUpperCase();
    }
  }
  return titleCaseWord(word);
}

/**
 * Whether a split segment is whitespace or an empty edge entry rather than a word.
 *
 * @param segment - One entry from splitting a name on whitespace runs.
 * @returns True for empty or whitespace-only segments.
 *
 * @example
 * isGap('  ')    // true
 * isGap('')      // true
 * isGap('Smith') // false
 */
function isGap(segment: string): boolean {
  return /^\s*$/.test(segment);
}

/**
 * Title-cases a person's name for display, but only when it's all one case.
 *
 * Some backends store names in all caps, while others preserve mixed case.
 * Mixed-case names ("MacDonald", "van der Berg") were cased on purpose and are
 * returned unchanged. Whitespace is preserved.
 *
 * Known limit: "MACDONALD" becomes "Macdonald"; guessing "Mac" prefixes would break names like "Machado".
 *
 * @param value - The name as stored by its source.
 * @returns The display-cased name, or `value` unchanged if it has mixed case.
 *
 * @example
 * formatPersonName('DELLA ALDEN')    // 'Della Alden'
 * formatPersonName('JOHN SMITH III') // 'John Smith III'
 * formatPersonName('MacDonald')      // 'MacDonald'
 */
export function formatPersonName(value: string): string {
  if (!value || !hasUniformCase(value)) {
    return value;
  }

  // Splitting on a capturing group keeps whitespace runs as segments, so spacing survives.
  const segments = value.split(/(\s+)/);

  // Only the last word can be a suffix, and only when another word precedes it; -1 = none.
  const wordIndices = segments
    .map((segment, index) => (isGap(segment) ? -1 : index))
    .filter((index) => index >= 0);
  const trailingIndex =
    wordIndices.length > 1 ? (wordIndices[wordIndices.length - 1] ?? -1) : -1;

  return segments
    .map((segment, index) =>
      isGap(segment)
        ? segment
        : formatNameWord(segment, index === trailingIndex),
    )
    .join("");
}
