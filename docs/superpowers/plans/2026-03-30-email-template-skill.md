# Email Template Generation Skill — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a Claude skill and verification script that deterministically converts markdown email notice content into email-client-safe HTML matching DC SUN Bucks branding.

**Architecture:** A Claude skill (`.claude/skills/email-template/SKILL.md`) defines exact HTML patterns for every markdown element type, referencing `email-templates/PreApprovalNotice.html` as the canonical reference implementation. A Node.js lint script (`email-templates/verify.mjs`) validates structural correctness of generated HTML. The skill orchestrates: generate HTML → run lint → render Playwright screenshots for visual review.

**Tech Stack:** Claude skill (markdown), Node.js (verify script), Playwright (screenshots)

---

## File Structure

| File | Responsibility |
|------|---------------|
| `.claude/skills/email-template/SKILL.md` | Skill definition: conversion rules, element patterns, workflow steps |
| `email-templates/verify.mjs` | Structural lint: validates generated HTML against email-safe rules |
| `email-templates/_snapshots/` | Directory for Playwright screenshots (gitignored) |
| `email-templates/test-inputs/PreApprovalNotice.md` | Markdown source for round-trip testing |

Files modified:
| File | Change |
|------|--------|
| `.gitignore` | Add `email-templates/_snapshots/` |

Files referenced (read-only):
| File | Purpose |
|------|---------|
| `email-templates/PreApprovalNotice.html` | Canonical reference implementation — every pattern the skill teaches comes from this file |

---

### Task 1: Create the structural lint script (`email-templates/verify.mjs`)

This comes first because the skill will invoke it as a verification step — we need it to exist before writing the skill. TDD: we write the lint, test it against the known-good PreApprovalNotice.html, and confirm it passes.

**Files:**
- Create: `email-templates/verify.mjs`

- [ ] **Step 1: Write the lint script**

Create `email-templates/verify.mjs`. The script:
- Takes a file path as a CLI argument: `node verify.mjs PreApprovalNotice.html`
- Parses the HTML as a string (no heavy dependencies — use regex/string checks, not a DOM parser, to keep it zero-dependency)
- Runs these checks, collecting all failures before reporting:

**Required structure checks:**
1. Has `<!DOCTYPE html>` declaration
2. Has `<html lang=` attribute
3. Has `<meta charset="UTF-8" />`
4. Has `<meta name="viewport"` tag
5. Has Outlook MSO conditional comment (`<!--[if mso]>`)
6. Outer table uses `role="presentation"`
7. No `<div>` elements used (tables only for layout)

**CSS safety checks:**
8. No `display: flex` anywhere
9. No `display: grid` anywhere
10. No `position: absolute` or `position: fixed`
11. No `@import` in styles
12. No `calc(` in styles
13. No `var(--` CSS custom properties

**Brand consistency checks:**
14. Font stack contains `Source Sans Pro` (primary font)
15. Only approved colors used. Scan all `color:`, `background-color:`, and `border` property values. Approved palette:
    - `#2a646d` (primary)
    - `#e0f7f6` (primary lightest)
    - `#ffbe2e` (secondary/accent)
    - `#1b1b1b` (text dark)
    - `#5c5c5c` (text muted)
    - `#71767a` (text light)
    - `#ffffff` (white)
    - `#f5f5f5` (background gray)
    - `#cccccc` (divider)
16. `max-width` of email container is `600px`

**Content safety checks:**
17. All `<a href=` tags include `style="color:` (inline link color)
18. All `<table` tags include `role="presentation"`, `cellspacing="0"`, `cellpadding="0"`, `border="0"`

**Output format:**
- On success: print `✅ All checks passed (N/N)` and exit 0
- On failure: print each failure as `❌ [check-name]: description` and exit 1
- Always print a summary line: `N/N checks passed`

See implementation code inline in Task 1 Step 1 of the reference below.

<details>
<summary>Full verify.mjs implementation</summary>

```javascript
#!/usr/bin/env node

// email-templates/verify.mjs
// Structural lint for email-safe HTML templates.
// Usage: node verify.mjs <path-to-html-file>

import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const filePath = process.argv[2];
if (!filePath) {
  console.error("Usage: node verify.mjs <path-to-html-file>");
  process.exit(2);
}

const html = readFileSync(resolve(filePath), "utf-8");
const failures = [];
let totalChecks = 0;

function check(name, condition, message) {
  totalChecks++;
  if (!condition) {
    failures.push({ name, message });
  }
}

// --- Required structure ---
check("doctype", /<!DOCTYPE html>/i.test(html), "Missing <!DOCTYPE html>");
check(
  "html-lang",
  /<html[^>]+lang\s*=/i.test(html),
  'Missing lang attribute on <html>',
);
check(
  "meta-charset",
  /<meta\s+charset\s*=\s*"UTF-8"/i.test(html),
  'Missing <meta charset="UTF-8" />',
);
check(
  "meta-viewport",
  /<meta\s+name\s*=\s*"viewport"/i.test(html),
  'Missing <meta name="viewport"> tag',
);
check(
  "mso-conditional",
  /<!--\[if mso\]>/i.test(html),
  "Missing Outlook MSO conditional comment",
);
check(
  "outer-table-role",
  /<table[^>]*role\s*=\s*"presentation"/i.test(html),
  'No table with role="presentation" found',
);
check(
  "no-div-layout",
  !/<div[\s>]/i.test(html),
  "<div> elements found — use tables for email layout",
);

// --- CSS safety ---
check(
  "no-flexbox",
  !/display\s*:\s*flex/i.test(html),
  "display:flex found — not supported in email clients",
);
check(
  "no-grid",
  !/display\s*:\s*grid/i.test(html),
  "display:grid found — not supported in email clients",
);
check(
  "no-position-absolute",
  !/position\s*:\s*(absolute|fixed)/i.test(html),
  "position:absolute/fixed found — not supported in email clients",
);
check(
  "no-css-import",
  !/@import\s/i.test(html),
  "@import found — not reliably supported in email clients",
);
check(
  "no-calc",
  !/calc\s*\(/i.test(html),
  "calc() found — not supported in email clients",
);
check(
  "no-css-vars",
  !/var\s*\(\s*--/i.test(html),
  "CSS custom properties (var(--)) found — not supported in email clients",
);

// --- Brand consistency ---
check(
  "font-source-sans",
  /Source Sans Pro/i.test(html),
  '"Source Sans Pro" not found in font declarations',
);

const approvedColors = new Set([
  "#2a646d",
  "#e0f7f6",
  "#ffbe2e",
  "#1b1b1b",
  "#5c5c5c",
  "#71767a",
  "#ffffff",
  "#f5f5f5",
  "#cccccc",
]);
const colorPattern =
  /(?:(?:background-)?color|border(?:-(?:top|right|bottom|left))?)\s*:\s*[^;]*?(#[0-9a-fA-F]{3,8})/g;
const foundColors = new Set();
let colorMatch;
while ((colorMatch = colorPattern.exec(html)) !== null) {
  foundColors.add(colorMatch[1].toLowerCase());
}
const unapprovedColors = [...foundColors].filter(
  (c) => !approvedColors.has(c),
);
check(
  "approved-colors-only",
  unapprovedColors.length === 0,
  `Unapproved colors found: ${unapprovedColors.join(", ")}. Approved: ${[...approvedColors].join(", ")}`,
);

check(
  "container-max-width",
  /max-width\s*:\s*600px/i.test(html),
  "Email container max-width is not 600px",
);

// --- Content safety ---
const anchorPattern = /<a\s[^>]*>/gi;
let anchorMatch;
const anchorsWithoutColor = [];
while ((anchorMatch = anchorPattern.exec(html)) !== null) {
  if (!/style\s*=\s*"[^"]*color\s*:/i.test(anchorMatch[0])) {
    anchorsWithoutColor.push(
      anchorMatch[0].substring(0, 80) +
        (anchorMatch[0].length > 80 ? "..." : ""),
    );
  }
}
check(
  "anchor-inline-color",
  anchorsWithoutColor.length === 0,
  `<a> tags without inline color style: ${anchorsWithoutColor.length} found`,
);

const tablePattern = /<table[^>]*>/gi;
let tableMatch;
const badTables = [];
while ((tableMatch = tablePattern.exec(html)) !== null) {
  const tag = tableMatch[0];
  const missingAttrs = [];
  if (!/role\s*=\s*"presentation"/i.test(tag))
    missingAttrs.push('role="presentation"');
  if (!/cellspacing\s*=\s*"0"/i.test(tag))
    missingAttrs.push('cellspacing="0"');
  if (!/cellpadding\s*=\s*"0"/i.test(tag))
    missingAttrs.push('cellpadding="0"');
  if (!/border\s*=\s*"0"/i.test(tag)) missingAttrs.push('border="0"');
  if (missingAttrs.length > 0) {
    badTables.push(`Missing ${missingAttrs.join(", ")}`);
  }
}
check(
  "table-attributes",
  badTables.length === 0,
  `Tables with missing attributes: ${badTables.map((b, i) => `\n  ${i + 1}. ${b}`).join("")}`,
);

// --- Report ---
if (failures.length === 0) {
  console.log(`✅ All checks passed (${totalChecks}/${totalChecks})`);
  process.exit(0);
} else {
  for (const f of failures) {
    console.log(`❌ [${f.name}]: ${f.message}`);
  }
  console.log(
    `\n${totalChecks - failures.length}/${totalChecks} checks passed`,
  );
  process.exit(1);
}
```

</details>

- [ ] **Step 2: Run the lint against PreApprovalNotice.html to verify it passes**

```bash
cd email-templates
node verify.mjs PreApprovalNotice.html
```

Expected: `✅ All checks passed (18/18)` and exit code 0.

If any checks fail, fix the lint script (the reference template is the source of truth — if it fails a check, the check is wrong, not the template).

- [ ] **Step 3: Verify the lint catches violations**

Create a minimal bad HTML file to confirm the lint detects problems:

```bash
cd email-templates
echo '<html><body><div style="display:flex; color:#ff0000; border-left: 4px solid #ff0000;">bad</div></body></html>' > _test-bad.html
node verify.mjs _test-bad.html
echo "exit: $?"
```

Expected: Multiple `❌` lines and exit code 1. Then clean up:

```bash
rm email-templates/_test-bad.html
```

- [ ] **Step 4: Commit**

```bash
git add email-templates/verify.mjs
git commit -m "Add email template structural lint script"
```

---

### Task 2: Add `_snapshots/` to `.gitignore`

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Add the gitignore entry**

Append to `.gitignore`:

```
# Email template verification screenshots
email-templates/_snapshots/
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "Gitignore email template verification screenshots"
```

---

### Task 3: Write the Claude skill (`.claude/skills/email-template/SKILL.md`)

This is the main deliverable. The skill codifies every HTML pattern from PreApprovalNotice.html into explicit conversion rules.

**Files:**
- Create: `.claude/skills/email-template/SKILL.md`

- [ ] **Step 1: Write the skill definition**

The skill must contain these sections (see the full skill content in the appendix below):

1. **Frontmatter** — name, description, allowed-tools, argument-hint
2. **Overview** — what the skill does
3. **Arguments** — template name (positional)
4. **Workflow** — 7-step process (receive markdown → identify variables → generate HTML → write file → lint → screenshot → present)
5. **Reference Implementation** — always read PreApprovalNotice.html first
6. **HTML Document Structure** — exact skeleton
7. **Section Patterns** — header bar, main body, do-not-reply, legal, confidentiality
8. **Element Conversion Rules** — paragraphs, bold, italic, links, headings, callout boxes, bullet lists, numbered lists, contact box, signature, dividers
9. **Approved Design Tokens** — color table
10. **Font Stack** — exact string
11. **Template Variables** — mapping conventions
12. **Verification Steps** — lint + screenshots
13. **Rules** — behavioral constraints

For the complete skill content, read `email-templates/PreApprovalNotice.html` and extract every unique pattern into a named rule with an exact HTML code snippet. Each pattern must match the reference implementation byte-for-byte in terms of structure, attributes, and style values.

Key rules to encode:
- **Reference implementation is truth:** When in doubt, read and match PreApprovalNotice.html
- **No creative interpretation:** Apply patterns mechanically
- **Content from markdown, structure from patterns:** All text from user input, all HTML from skill patterns
- **Legal content varies:** Do NOT copy legal text from reference — use what the markdown provides
- **Lint must pass:** Never present a template that fails verify.mjs
- **Preserve all content:** Never omit, summarize, or rephrase markdown content — government notices have legally reviewed language
- **HTML entities for special characters:** `&ldquo;` `&rdquo;` for smart quotes, `&ndash;` for en-dash, `&#8226;` for bullets, numeric entities for non-Latin scripts

See Appendix A for the complete skill file content.

- [ ] **Step 2: Verify the skill file is valid**

Read back the file and confirm:
- Frontmatter has name, description, allowed-tools, argument-hint
- All patterns match the reference implementation exactly
- No patterns are missing (compare section-by-section against PreApprovalNotice.html)

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/email-template/SKILL.md
git commit -m "Add email template generation skill with conversion rules and verification workflow"
```

---

### Task 4: Test the skill end-to-end by regenerating PreApprovalNotice

This is the critical validation step. We use the skill to regenerate the template we already have and confirm it produces equivalent output.

**Files:**
- Create: `email-templates/test-inputs/PreApprovalNotice.md`

- [ ] **Step 1: Save the markdown source as a test input file**

Create `email-templates/test-inputs/PreApprovalNotice.md` with the original Google Doc markdown content. This is the exact text that was used to generate the reference implementation. It should be committed so future round-trip tests are reproducible.

The content is the pre-approval notice starting with "Dear Parent/Guardian:" through the confidentiality notice, exactly as pasted from the Google Doc. Include the subject line as a markdown heading at the top.

- [ ] **Step 2: Save a backup of the current reference**

```bash
cp email-templates/PreApprovalNotice.html email-templates/PreApprovalNotice.reference.html
```

- [ ] **Step 3: Invoke the skill with the test input**

Use the markdown content from `email-templates/test-inputs/PreApprovalNotice.md` as input to the skill, with template name `PreApprovalNotice`.

- [ ] **Step 4: Compare the output to the reference**

```bash
diff email-templates/PreApprovalNotice.html email-templates/PreApprovalNotice.reference.html
```

The outputs should be structurally equivalent. Minor whitespace differences are acceptable. Structural differences (different table nesting, different style values, missing sections) indicate the skill patterns need updating.

- [ ] **Step 5: Fix any skill pattern gaps**

If the diff reveals structural differences, update the skill's patterns to match and re-run. Iterate until the skill produces output matching the reference.

- [ ] **Step 6: Clean up and commit**

```bash
rm email-templates/PreApprovalNotice.reference.html
git add email-templates/test-inputs/PreApprovalNotice.md
git commit -m "Add markdown test input for pre-approval notice round-trip testing"
```

If skill pattern fixes were needed:
```bash
git add .claude/skills/email-template/SKILL.md
git commit -m "Refine email template skill patterns based on round-trip validation"
```

---

### Task 5: Capture baseline screenshots for the reference template

**Files:**
- Create: `email-templates/_snapshots/PreApprovalNotice-375.png`
- Create: `email-templates/_snapshots/PreApprovalNotice-600.png`

- [ ] **Step 1: Create snapshots directory**

```bash
mkdir -p email-templates/_snapshots
```

- [ ] **Step 2: Capture screenshots at both widths**

Use Playwright to navigate to `file:///.../email-templates/PreApprovalNotice.html` and capture:
- 375px width → `email-templates/_snapshots/PreApprovalNotice-375.png`
- 600px width → `email-templates/_snapshots/PreApprovalNotice-600.png`

- [ ] **Step 3: Visual review**

Present both screenshots to the user for approval. These become the visual baseline that future templates are compared against.

No commit needed — snapshots are gitignored.

---

## Appendix A: Complete Skill File Content

Write the following content verbatim to `.claude/skills/email-template/SKILL.md`:

````markdown
---
name: email-template
description: Convert markdown email notice content into email-client-safe HTML matching DC SUN Bucks branding. Generates HTML, runs structural lint, and captures verification screenshots.
allowed-tools: Read, Write, Bash(node:*), Bash(mkdir:*), Bash(ls:*), Bash(rm:*), mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_close
argument-hint: <template-name>
---

# Email Template Generator

Convert markdown email notice content into email-client-safe HTML with DC SUN Bucks branding.

## Overview

This skill takes markdown content (typically pasted from a Google Doc) and produces a complete `.html` email template in `email-templates/`. It enforces consistent structure, styling, and branding across all notice types by following exact HTML patterns derived from the canonical reference implementation.

## Arguments

Parse `$ARGUMENTS` as:

| Position | Name | Example | Required |
|----------|------|---------|----------|
| 1 | Template name | `PreApprovalNotice` | Yes |

The template name is used for the output filename: `email-templates/<TemplateName>.html`

If no arguments provided, ask the user for a template name.

## Workflow

1. **Receive markdown content** — Ask the user to paste the email notice content as markdown if not already provided in the conversation.
2. **Read the reference implementation** — ALWAYS read `email-templates/PreApprovalNotice.html` first. This is the canonical source of truth for every pattern.
3. **Identify template variables** — Scan for placeholder text like `<FIRST NAME>`, `<ISSUE DATE>`, etc. Convert these to `{{CamelCase}}` Handlebars-style variables.
4. **Generate HTML** — Convert the markdown to email-safe HTML following the patterns below exactly.
5. **Write the file** — Save to `email-templates/<TemplateName>.html`
6. **Run structural lint** — Execute `node email-templates/verify.mjs email-templates/<TemplateName>.html`. If it fails, fix the issues and re-run.
7. **Capture screenshots** — Use Playwright to render the HTML at 375px (mobile) and 600px (desktop) widths and save screenshots for visual review.
8. **Present for review** — Show the user the screenshots and ask for approval.

## HTML Document Structure

Every email template MUST follow this exact structure:

```
<!DOCTYPE html>
<html lang="{{Language}}">
  <head>
    <!-- meta tags, MSO conditional, <style> block -->
  </head>
  <body>
    <table>                          ← outer wrapper (background)
      <tr><td>
        <!--[if mso]>table<![endif]--> ← Outlook max-width fix
        <table>                      ← email container (600px)
          <tr> HEADER BAR </tr>
          <tr> MAIN BODY </tr>
          <tr> DO NOT REPLY + LANGUAGE HELP </tr>
          <tr> LEGAL SECTIONS (one <tr> per section) </tr>
          <tr> CONFIDENTIALITY NOTICE </tr>
        </table>
        <!--[if mso]>close<![endif]-->
      </td></tr>
    </table>
  </body>
</html>
```

### Head Section

Copy the `<head>` section from the reference implementation exactly, including:
- Meta tags (charset, viewport, X-UA-Compatible)
- MSO conditional comment for Outlook
- `<style>` block with reset styles and responsive media queries
- Update only the `<title>` to match the new notice's subject line

### Outer Wrapper

The outermost table provides the gray background. Copy this structure exactly from the reference.

### Email Container

`max-width: 600px`, `width: 100%`, white background. Wrapped in MSO conditional tables for Outlook.

## Section Patterns

### HEADER BAR

Primary-colored bar with logo placeholder:

```html
<tr>
  <td style="background-color: #2a646d; padding: 20px 30px; text-align: left;">
    {{LogoHtml}}
  </td>
</tr>
```

### MAIN BODY

Wrapped in a single `<td class="email-body">` with the standard font stack and sizing:

```html
<tr>
  <td
    class="email-body"
    style="
      padding: 32px 30px;
      font-family: 'Source Sans Pro', -apple-system,
        BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue',
        Arial, sans-serif;
      font-size: 16px;
      line-height: 1.5;
      color: #1b1b1b;
    "
  >
    <!-- All main body content goes here -->
  </td>
</tr>
```

### DO NOT REPLY + LANGUAGE HELP

Gray background row, centered, smaller text:

```html
<tr>
  <td style="background-color: #f5f5f5; padding: 24px 30px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 14px; line-height: 1.6; color: #5c5c5c; text-align: center;">
    <p style="margin: 0 0 16px 0; font-style: italic">
      Please do NOT reply to this automated message...
    </p>
    <p style="margin: 0 0 8px 0">
      If you need help understanding this notice, please visit
      <a href="https://sunbucks.dc.gov" style="color: #2a646d; text-decoration: underline">sunbucks.dc.gov</a>.
    </p>
    <!-- Each language help line as its own <p style="margin: 0 0 8px 0"> -->
    <!-- Last language line uses margin: 0 0 0 0 -->
  </td>
</tr>
```

The actual language translations come from the markdown input, not from the reference.

### LEGAL SECTIONS

Each legal section (Non-Discrimination, Appeal Rights, etc.) is its own `<tr>`. Content varies per notice — use the text from the markdown input.

```html
<tr>
  <td class="legal-section" style="padding: 24px 30px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 12px; line-height: 1.5; color: #5c5c5c;">
    <p style="margin: 0 0 8px 0; font-weight: 700; font-size: 13px; color: #1b1b1b;">
      Section Title
    </p>
    <p style="margin: 0 0 12px 0">Legal text content...</p>
  </td>
</tr>
```

For continuation sections (e.g., Appeal Rights follows Non-Discrimination), use top padding of 0:
```html
style="padding: 0 30px 24px 30px; ..."
```

Bold callouts within legal sections:
```html
<p style="margin: 0 0 12px 0; font-weight: 700; color: #1b1b1b;">
  Important bold legal text
</p>
```

Sub-headings within legal sections:
```html
<p style="margin: 0 0 8px 0; font-weight: 700; color: #1b1b1b;">
  Sub-heading Text
</p>
```

### CONFIDENTIALITY NOTICE

Always the last row. Gray background, smallest type:

```html
<tr>
  <td style="padding: 16px 30px 24px 30px; background-color: #f5f5f5; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 11px; line-height: 1.5; color: #71767a;">
    <p style="margin: 0 0 4px 0; font-weight: 700">CONFIDENTIALITY NOTICE:</p>
    <p style="margin: 0">Notice text from the markdown input...</p>
  </td>
</tr>
```

## Element Conversion Rules

These rules define how each markdown element maps to email HTML. Follow them exactly.

### Paragraphs

```html
<p style="margin: 0 0 16px 0">Text content here.</p>
```

Last paragraph before a new section: use `margin: 0 0 24px 0` for extra spacing.

### Bold text

```html
<strong>bold text</strong>
```

### Italic text

```html
<span style="font-style: italic">italic text</span>
```

Or when the entire paragraph is italic:
```html
<p style="margin: 0 0 16px 0; font-style: italic">italic paragraph</p>
```

### FAQ Question/Answer Pairs

FAQ questions use bold+italic styling with tight bottom margin, followed by the answer as a normal paragraph:

```html
<p style="margin: 16px 0 4px 0; font-weight: 700; font-style: italic;">
  Question text here?
</p>
<p style="margin: 0 0 16px 0">
  Answer text here.
</p>
```

The last FAQ answer before a new section uses `margin: 0 0 24px 0`.

### Links

ALWAYS include inline color style. For body links:
```html
<a href="https://example.com" style="color: #2a646d; text-decoration: underline">link text</a>
```

For links on dark backgrounds (e.g., contact box):
```html
<a href="https://example.com" style="color: #ffffff; text-decoration: underline">link text</a>
```

For `tel:` links, prefix with `+1` and remove formatting:
```html
<a href="tel:+12028884834" style="color: #2a646d; text-decoration: underline">(202) 888-4834</a>
```

For `mailto:` links:
```html
<a href="mailto:address@example.com" style="color: #2a646d; text-decoration: underline">address@example.com</a>
```

### Section Headings (with gold accent underline)

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" style="margin: 0 0 8px 0">
  <tr>
    <td style="border-bottom: 2px solid #ffbe2e; padding-bottom: 6px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 20px; font-weight: 700; color: #1b1b1b;">
      Section Heading Text
    </td>
  </tr>
</table>
```

### Callout Box (highlighted info)

Used for key information that should stand out (e.g., SEBT ID):

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" class="sebt-id-box" style="margin: 0 0 24px 0">
  <tr>
    <td style="background-color: #e0f7f6; border-left: 4px solid #2a646d; padding: 16px 24px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 18px; font-weight: 700; color: #1b1b1b;">
      Callout content here
    </td>
  </tr>
</table>
```

### Bullet Lists (main body)

Each bullet is a table row with two cells — one for the bullet character, one for the text. Uses main body font size (16px) and text color (#1b1b1b):

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" style="margin: 16px 0 24px 0">
  <tr>
    <td width="6" valign="top" style="font-family: 'Source Sans Pro', sans-serif; font-size: 16px; line-height: 1.5; color: #1b1b1b; padding-right: 10px;">&#8226;</td>
    <td style="font-family: 'Source Sans Pro', sans-serif; font-size: 16px; line-height: 1.5; color: #1b1b1b; padding-bottom: 12px;">
      Bullet item text here.
    </td>
  </tr>
  <!-- more rows... -->
  <!-- LAST row: padding-bottom: 0 instead of padding-bottom: 12px -->
</table>
```

### Bullet Lists (legal sections)

Same structure as main body bullets but with legal section styling — smaller font (12px) and muted color (#5c5c5c):

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin: 0 0 12px 0">
  <tr>
    <td valign="top" style="font-family: 'Source Sans Pro', sans-serif; font-size: 12px; line-height: 1.5; color: #5c5c5c; padding: 0 8px 4px 0;">&#8226;</td>
    <td style="font-family: 'Source Sans Pro', sans-serif; font-size: 12px; line-height: 1.5; color: #5c5c5c; padding-bottom: 4px;">
      Legal bullet item text.
    </td>
  </tr>
  <!-- LAST row: padding: 0 8px 0 0 on bullet cell, no padding-bottom on text cell -->
</table>
```

### Numbered Lists (legal sections)

Same as legal bullet lists but with number text instead of `&#8226;`:

```html
<td valign="top" style="font-family: 'Source Sans Pro', sans-serif; font-size: 12px; line-height: 1.5; color: #5c5c5c; padding: 0 8px 4px 0;">1.</td>
```

### Contact Box (CTA)

Primary-colored box with centered text and links. Uses emoji entities for icons:

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" class="contact-box" style="margin: 0 0 24px 0">
  <tr>
    <td style="background-color: #2a646d; border-radius: 6px; padding: 20px 24px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 16px; line-height: 1.6; color: #ffffff; text-align: center;">
      <strong style="font-size: 18px">More Questions?</strong><br />
      Want to opt-in to receive email and SMS communications about your benefits?<br /><br />
      &#x1F310; Log online: <a href="https://sunbucks.dc.gov" style="color: #ffffff; text-decoration: underline">sunbucks.dc.gov</a><br />
      &#x1F4DE; Call: <a href="tel:+12028884834" style="color: #ffffff; text-decoration: underline">(202) 888-4834</a>
    </td>
  </tr>
</table>
```

Emoji entities used: `&#x1F310;` (globe) for web links, `&#x1F4DE;` (phone) for phone numbers.

### Signature Block

```html
<p style="margin: 0 0 4px 0">Regards,</p>
<p style="margin: 0 0 24px 0"><strong>DC SUN Bucks Program</strong></p>
```

### Divider with Footer Contact Info

Thin gray line followed by smaller muted text:

```html
<table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%">
  <tr>
    <td style="border-top: 1px solid #cccccc; padding-top: 20px; font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #5c5c5c;">
      <p style="margin: 0 0 4px 0">Portal: <a href="https://sunbucks.dc.gov" style="color: #2a646d; text-decoration: underline">sunbucks.dc.gov</a></p>
      <p style="margin: 0 0 4px 0">Phone: <a href="tel:+12028884834" style="color: #2a646d; text-decoration: underline">(202) 888-4834</a></p>
      <p style="margin: 0 0 12px 0">TTY/TDD: 711 Monday through Friday 9AM to 4PM</p>
      <p style="margin: 0 0 0 0">DC SUN Bucks Program<br />P.O. Box 90060<br />Washington, DC 20002</p>
    </td>
  </tr>
</table>
```

## Approved Design Tokens

These are the ONLY colors permitted in email templates:

| Token | Hex | Usage |
|-------|-----|-------|
| Primary | `#2a646d` | Header bar, links, callout border, contact box bg |
| Primary Lightest | `#e0f7f6` | Callout box background |
| Secondary | `#ffbe2e` | Section heading underline accent |
| Text Dark | `#1b1b1b` | Body text, headings |
| Text Muted | `#5c5c5c` | Footer text, legal sections |
| Text Light | `#71767a` | Confidentiality notice |
| White | `#ffffff` | Email container bg, text on dark backgrounds |
| Background | `#f5f5f5` | Page background, do-not-reply section, confidentiality bg |
| Divider | `#cccccc` | Horizontal rule / divider |

## Font Stack

Always use this exact font stack for all text:
```
'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif
```

Shortened form acceptable in bullet/list cells: `'Source Sans Pro', sans-serif`

## Template Variables

Convert placeholder text from the markdown to `{{CamelCase}}` variables:
- `<FIRST NAME>` or `<Child FN>` → `{{ChildFirstName}}`
- `<LAST NAME>` or `<Child LN>` → `{{ChildLastName}}`
- `<ISSUE DATE>` or `[ISSUANCE DATE]` → `{{IssuanceDate}}`
- `<SEBT ID>` → `{{SebtId}}`
- `<MONTH DAY, YEAR>` → `{{BenefitExpirationDate}}`

Always include `{{Language}}` on the `<html>` tag and `{{LogoHtml}}` in the header bar. If new placeholders appear in the markdown, follow the CamelCase convention and document them in a comment at the top of the generated HTML.

## Verification Steps

After generating the HTML file:

### Step 1: Run structural lint

```bash
node email-templates/verify.mjs email-templates/<TemplateName>.html
```

If any checks fail, fix the HTML and re-run. Do NOT skip lint failures.

### Step 2: Capture verification screenshots

```bash
mkdir -p email-templates/_snapshots
```

Use Playwright to:
1. Navigate to the HTML file via `file://` URL
2. Resize to **375px** width (mobile), capture full-page screenshot → `email-templates/_snapshots/<TemplateName>-375.png`
3. Resize to **600px** width (desktop), capture full-page screenshot → `email-templates/_snapshots/<TemplateName>-600.png`

Present the screenshots to the user for visual review.

### Step 3: Cross-reference with reference implementation

If this is not the first template, also capture (or show) the reference template's screenshots at the same widths so the user can confirm visual consistency across templates.

## Rules

- **Reference implementation is truth:** When in doubt about any pattern, read `email-templates/PreApprovalNotice.html` and match it.
- **No creative interpretation:** Apply the patterns mechanically. Do not invent new element styles, spacing values, or color combinations.
- **Content from markdown, structure from patterns:** All text content comes from the user's markdown input. All HTML structure and styling comes from the patterns defined above.
- **Legal content varies:** Do NOT copy legal text from the reference implementation. Each notice has its own legal language — use what the markdown provides.
- **Lint must pass:** Never present a template to the user that fails `verify.mjs`.
- **Preserve all content:** Never omit, summarize, or rephrase any content from the markdown input. Government notices have legally reviewed language.
- **HTML entities for special characters:** Use `&ldquo;` `&rdquo;` for smart quotes, `&ndash;` for en-dash, `&#8226;` for bullets, `&rsquo;` for apostrophes. Use numeric HTML entities for non-Latin scripts (Amharic, Korean, Chinese, Vietnamese).
- **No `<div>` elements:** Use `<table>` for all layout. This ensures maximum email client compatibility.
- **Section-aware list styling:** Bullet and numbered lists in the main body use 16px/#1b1b1b. Lists in legal sections use 12px/#5c5c5c. Always use the correct variant for the section context.
````
