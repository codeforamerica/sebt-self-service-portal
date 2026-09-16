#!/usr/bin/env node
/**
 * Email template structural lint script.
 *
 * Checks an HTML email template for email-safe structure, CSS safety,
 * brand consistency, and content safety. Collects all failures before
 * reporting so the author can fix everything in one pass.
 *
 * Usage: node verify.mjs <path-to-html-file>
 * Exit codes:
 *   0 — all checks passed
 *   1 — one or more checks failed
 *   2 — missing or invalid argument
 */

import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// ---------------------------------------------------------------------------
// Argument handling
// ---------------------------------------------------------------------------

const [, , filePath] = process.argv;

if (!filePath) {
  console.error('Usage: node verify.mjs <path-to-html-file>');
  process.exit(2);
}

let html;
try {
  html = readFileSync(resolve(filePath), 'utf8');
} catch (err) {
  console.error(`Error reading file: ${err.message}`);
  process.exit(2);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const failures = [];

/**
 * Register a check result. If the check fails, records the failure message.
 * Returns true when the check passed, false when it failed.
 *
 * @param {string} name   — short identifier shown in output (e.g. "doctype")
 * @param {boolean} passed — whether the check succeeded
 * @param {string} description — human-readable failure description
 */
function check(name, passed, description) {
  if (!passed) {
    failures.push({ name, description });
  }
  return passed;
}

// ---------------------------------------------------------------------------
// Approved brand color palette (lowercase hex, 6 digits)
// ---------------------------------------------------------------------------

const APPROVED_COLORS = new Set([
  '#2a646d',
  '#e0f7f6',
  '#ffbe2e',
  '#1b1b1b',
  '#5c5c5c',
  '#71767a',
  '#ffffff',
  '#f5f5f5',
  '#cccccc',
]);

/**
 * Extract all hex color values from a string.
 * Returns an array of lowercase 6-digit hex strings (e.g. "#ff0000").
 * 3-digit shorthand is expanded to 6 digits.
 */
function extractHexColors(source) {
  const matches = source.match(/#[0-9a-fA-F]{3,8}/g) ?? [];
  const colors = [];
  for (const raw of matches) {
    const hex = raw.toLowerCase();
    // Only process 3-digit or 6-digit hex values (skip 4/5/7/8-digit variants
    // which are RGBA or other non-standard forms not used in email CSS).
    if (hex.length === 4) {
      // Expand 3-digit to 6-digit: #abc → #aabbcc
      colors.push(
        '#' + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3],
      );
    } else if (hex.length === 7) {
      colors.push(hex);
    }
    // Ignore 4-digit (#rgba), 5-digit, 8-digit (#rrggbbaa) variants.
  }
  return colors;
}

/**
 * Extract all values for a given CSS property from inline styles and <style>
 * blocks. Returns an array of raw value strings.
 *
 * @param {string} source — full HTML string
 * @param {string} property — CSS property name (e.g. "color", "background-color")
 */
function extractCssPropertyValues(source, property) {
  // Escape hyphens in the property name for use in a regex.
  const escapedProp = property.replace(/-/g, '\\-');
  const pattern = new RegExp(`${escapedProp}\\s*:\\s*([^;}"']+)`, 'gi');
  const values = [];
  for (const match of source.matchAll(pattern)) {
    values.push(match[1].trim());
  }
  return values;
}

// ---------------------------------------------------------------------------
// Required structure checks (1–7)
// ---------------------------------------------------------------------------

// 1. DOCTYPE declaration
check(
  'doctype',
  /<!DOCTYPE\s+html/i.test(html),
  'Missing <!DOCTYPE html> declaration',
);

// 2. <html lang= attribute
check(
  'html-lang',
  /<html[^>]+lang\s*=/i.test(html),
  'Missing lang attribute on <html> element',
);

// 3. <meta charset="UTF-8" />
check(
  'meta-charset',
  /<meta[^>]+charset\s*=\s*["']?UTF-8["']?/i.test(html),
  'Missing <meta charset="UTF-8" />',
);

// 4. <meta name="viewport"
check(
  'meta-viewport',
  /<meta[^>]+name\s*=\s*["']viewport["']/i.test(html),
  'Missing <meta name="viewport" ...> tag',
);

// 5. Outlook MSO conditional comment
check(
  'mso-conditional',
  /<!--\[if mso\]>/i.test(html),
  'Missing Outlook MSO conditional comment (<!--[if mso]>)',
);

// 6. Outer table uses role="presentation"
// The first <table> element in the body should have role="presentation".
const firstTableMatch = html.match(/<table\b[^>]*>/i);
check(
  'outer-table-role',
  firstTableMatch !== null &&
    /role\s*=\s*["']presentation["']/i.test(firstTableMatch[0]),
  'Outer table is missing role="presentation"',
);

// 7. No <div> elements (tables only for layout)
check(
  'no-divs',
  !/<div\b/i.test(html),
  '<div> elements found — use tables for layout in email',
);

// ---------------------------------------------------------------------------
// CSS safety checks (8–13)
// ---------------------------------------------------------------------------

// 8. No display: flex
check(
  'no-flex',
  !/display\s*:\s*flex/i.test(html),
  'CSS "display: flex" found — not supported in email clients',
);

// 9. No display: grid
check(
  'no-grid',
  !/display\s*:\s*grid/i.test(html),
  'CSS "display: grid" found — not supported in email clients',
);

// 10. No position: absolute or position: fixed
check(
  'no-position',
  !/position\s*:\s*(absolute|fixed)/i.test(html),
  'CSS "position: absolute" or "position: fixed" found — not safe for email',
);

// 11. No @import in styles
check(
  'no-import',
  !/@import\b/i.test(html),
  'CSS @import found — not supported in email clients',
);

// 12. No calc()
check(
  'no-calc',
  !/calc\s*\(/i.test(html),
  'CSS calc() found — not reliably supported in email clients',
);

// 13. No var(-- CSS custom properties
check(
  'no-css-vars',
  !/var\s*\(\s*--/i.test(html),
  'CSS custom properties (var(--...)) found — not supported in email clients',
);

// ---------------------------------------------------------------------------
// Brand consistency checks (14–16)
// ---------------------------------------------------------------------------

// 14. Font stack contains "Source Sans Pro"
check(
  'font-source-sans-pro',
  /Source Sans Pro/i.test(html),
  'Font stack does not contain "Source Sans Pro"',
);

// 15. Only approved colors used
// Scan color:, background-color:, and border* properties for hex values.
const colorProperties = ['color', 'background-color', 'background'];
// For border properties we use a broader scan below.
const colorValues = colorProperties.flatMap((prop) =>
  extractCssPropertyValues(html, prop),
);

// Capture all border-* property values (border, border-left, border-top, etc.)
const borderValues = [];
for (const match of html.matchAll(/border[\w-]*\s*:\s*([^;}"']+)/gi)) {
  borderValues.push(match[1].trim());
}

const allColorSource = [...colorValues, ...borderValues].join(' ');
const foundHexColors = extractHexColors(allColorSource);
const unapprovedColors = [
  ...new Set(foundHexColors.filter((c) => !APPROVED_COLORS.has(c))),
];

check(
  'approved-colors',
  unapprovedColors.length === 0,
  `Unapproved color(s) found: ${unapprovedColors.join(', ')}`,
);

// 16. max-width of email container is 600px
check(
  'max-width-600',
  /max-width\s*:\s*600px/i.test(html),
  'Email container max-width is not 600px',
);

// ---------------------------------------------------------------------------
// Content safety checks (17–18)
// ---------------------------------------------------------------------------

// 17. All <a href= tags include style="color:
// Extract every opening anchor tag that has an href attribute.
const anchorTags = [];
for (const match of html.matchAll(/<a\b[^>]+href\s*=[^>]*>/gi)) {
  anchorTags.push(match[0]);
}

const anchorsWithoutColor = anchorTags.filter(
  (tag) => !/style\s*=\s*["'][^"']*color\s*:/i.test(tag),
);

check(
  'link-color',
  anchorsWithoutColor.length === 0,
  `${anchorsWithoutColor.length} <a href> tag(s) missing inline style="color:..."`,
);

// 18. All <table tags include role="presentation", cellspacing="0",
//     cellpadding="0", border="0"
const tableTags = [];
for (const match of html.matchAll(/<table\b[^>]*>/gi)) {
  tableTags.push(match[0]);
}

const REQUIRED_TABLE_ATTRS = [
  { attr: 'role="presentation"', pattern: /role\s*=\s*["']presentation["']/i },
  { attr: 'cellspacing="0"', pattern: /cellspacing\s*=\s*["']0["']/i },
  { attr: 'cellpadding="0"', pattern: /cellpadding\s*=\s*["']0["']/i },
  { attr: 'border="0"', pattern: /border\s*=\s*["']0["']/i },
];

const tableIssues = [];
for (const tag of tableTags) {
  const missing = REQUIRED_TABLE_ATTRS.filter(
    ({ pattern }) => !pattern.test(tag),
  ).map(({ attr }) => attr);
  if (missing.length > 0) {
    tableIssues.push(`Missing ${missing.join(', ')} on: ${tag.slice(0, 80).trim()}...`);
  }
}

check(
  'table-attrs',
  tableIssues.length === 0,
  `${tableIssues.length} <table> tag(s) missing required attributes:\n  ${tableIssues.join('\n  ')}`,
);

// ---------------------------------------------------------------------------
// Image integrity checks (19)
// ---------------------------------------------------------------------------

// 19. Embedded base64 image data URIs match the canonical asset byte-for-byte.
// Long opaque base64 blobs are unreliable to transcribe by hand, so we compare
// every embedded data URI in the HTML against a canonical reference file.
// If the canonical asset is missing, the check is skipped (opt-in).
const verifyDir = dirname(fileURLToPath(import.meta.url));
const canonicalAssetPath = resolve(verifyDir, 'assets/sun-bucks-logo-inline.html');

if (!existsSync(canonicalAssetPath)) {
  console.log(
    'logo-base64-integrity: canonical asset not found, skipped',
  );
} else {
  const canonicalSource = readFileSync(canonicalAssetPath, 'utf8');
  const canonicalMatch = canonicalSource.match(
    /data:image\/png;base64,([A-Za-z0-9+/=]+)/,
  );
  const canonicalBase64 = canonicalMatch ? canonicalMatch[1] : null;

  // Find every base64 data URI in the input HTML.
  const embeddedBase64s = [];
  for (const match of html.matchAll(
    /data:image\/png;base64,([A-Za-z0-9+/=]+)/g,
  )) {
    embeddedBase64s.push(match[1]);
  }

  if (embeddedBase64s.length === 0) {
    // No embedded data URIs — trivially passes.
    check('logo-base64-integrity', true, '');
  } else if (!canonicalBase64) {
    // Canonical asset exists but didn't yield a base64 string; treat as failure
    // since we cannot validate the embedded URIs.
    check(
      'logo-base64-integrity',
      false,
      'Canonical asset assets/sun-bucks-logo-inline.html does not contain a parseable base64 data URI',
    );
  } else {
    const mismatch = embeddedBase64s.find((b) => b !== canonicalBase64);
    check(
      'logo-base64-integrity',
      mismatch === undefined,
      mismatch === undefined
        ? ''
        : `Embedded base64 image data does not match canonical assets/sun-bucks-logo-inline.html (found length=${mismatch.length}, expected length=${canonicalBase64.length}) — possible truncation, duplication, or hand-paste corruption. Use a script-based substitution rather than typing the base64 directly.`,
    );
  }
}

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

const totalChecks = 19;
const passedCount = totalChecks - failures.length;

if (failures.length === 0) {
  console.log(`✅ All checks passed (${passedCount}/${totalChecks})`);
  process.exit(0);
} else {
  for (const { name, description } of failures) {
    console.error(`❌ [${name}]: ${description}`);
  }
  console.error(`\n${passedCount}/${totalChecks} checks passed`);
  process.exit(1);
}
