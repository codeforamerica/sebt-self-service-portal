#!/usr/bin/env node
'use strict';

// PreToolUse hook — blocks Edit/Write calls that would introduce PII
// (email addresses, SSNs, credit card numbers) into version-controlled files.
// Gitignored files are skipped since they won't be committed.
//
// PII regex patterns sourced from established FOSS libraries:
//   - Email:       wrannaman/redactpii-node (tightened domain validation)
//   - SSN:         solvvy/redact-pii (production-proven, covers dash/dot/space delimiters)
//   - Credit card: solvvy/redact-pii + microsoft/presidio (Luhn checksum validation)
//
// Exit codes:
//   0  Content is clean (or file is gitignored); allow the tool call
//   2  PII detected; block the tool call (stderr message shown to Claude)

const { execFileSync } = require('node:child_process');
const { readFileSync } = require('node:fs');

// ---------------------------------------------------------------------------
// Parse tool input from stdin
// ---------------------------------------------------------------------------
let input;
try {
  const raw = readFileSync('/dev/stdin', 'utf8');
  input = JSON.parse(raw);
} catch {
  process.stderr.write('PII hook: failed to parse tool input JSON\n');
  process.exit(2); // Fail closed — block if we can't parse
}

const toolInput = input.tool_input || {};
const filePath = toolInput.file_path || '';
const content = toolInput.new_string || toolInput.content || '';

// ---------------------------------------------------------------------------
// Skip gitignored files — they won't be committed, so PII there is harmless.
// This prevents the hook from blocking edits to settings.local.json and other
// personal config files that may contain paths with email addresses.
// ---------------------------------------------------------------------------
if (filePath) {
  try {
    execFileSync('git', ['check-ignore', '-q', '--', filePath], {
      stdio: 'ignore',
    });
    process.exit(0);
  } catch {
    // Not gitignored — proceed with PII check
  }
}

if (!content) {
  process.exit(0);
}

// ---------------------------------------------------------------------------
// PII detection patterns
// ---------------------------------------------------------------------------
const findings = [];

// --- Email addresses --------------------------------------------------------
// Pattern: wrannaman/redactpii-node — tightened domain part vs. solvvy original
const EMAIL_RE =
  /([a-z0-9_\-.+]+)@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?/gi;

// Domains that appear in test fixtures, commit trailers, and example code
const SAFE_EMAIL_RE =
  /@(example\.(com|org|net)|test\.(com|org|net)|localhost|users\.noreply\.github\.com|anthropic\.com)$/i;
const NOREPLY_RE = /^noreply@/i;

// Known government program contact addresses required verbatim in legal notices.
// These are institutional addresses, not personal PII.
const GOVT_PROGRAM_EMAILS_RE =
  /^(dc\.oara@dc\.gov|program\.intake@usda\.gov|sunbucksverify@dc\.gov)$/i;

const emails = [...new Set((content.match(EMAIL_RE) || []))]
  .filter((e) => !SAFE_EMAIL_RE.test(e) && !NOREPLY_RE.test(e) && !GOVT_PROGRAM_EMAILS_RE.test(e));

if (emails.length) {
  findings.push(`Email(s): ${emails.join(', ')}`);
}

// --- US Social Security Numbers ---------------------------------------------
// Pattern: solvvy/redact-pii — covers dash, dot, and space delimiters
const SSN_RE = /\b\d{3}[ -.]\d{2}[ -.]\d{4}\b/g;

const ssns = [...new Set(content.match(SSN_RE) || [])];
if (ssns.length) {
  findings.push(`Possible SSN(s): ${ssns.join(', ')}`);
}

// --- Credit card numbers ----------------------------------------------------
// Pattern: solvvy/redact-pii (16-digit + 15-digit Amex format)
// Validated with Luhn checksum per microsoft/presidio approach
const CC_RE =
  /\b\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{4}\b|\b\d{4}[ -]?\d{6}[ -]?\d{5}\b/g;

function luhnCheck(cardString) {
  const digits = cardString.replace(/\D/g, '');
  if (digits.length < 13 || digits.length > 19) return false;

  let sum = 0;
  let alternate = false;
  for (let i = digits.length - 1; i >= 0; i--) {
    let n = parseInt(digits[i], 10);
    if (alternate) {
      n *= 2;
      if (n > 9) n -= 9;
    }
    sum += n;
    alternate = !alternate;
  }
  return sum % 10 === 0;
}

const ccs = [...new Set((content.match(CC_RE) || []))]
  .filter((cc) => luhnCheck(cc));

if (ccs.length) {
  findings.push(`Possible credit card number(s): ${ccs.join(', ')}`);
}

// ---------------------------------------------------------------------------
// Report findings
// ---------------------------------------------------------------------------
if (findings.length) {
  const msg = [
    'PII detected \u2014 tool call blocked.',
    ...findings.map((f) => `  ${f}`),
    'If this is intentional test data, ask the user to approve the tool call.',
  ].join('\n');
  process.stderr.write(msg + '\n');
  process.exit(2);
}

process.exit(0);
