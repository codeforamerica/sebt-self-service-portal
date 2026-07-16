---
name: email-template
description: Convert markdown email notice content into email-client-safe HTML matching DC SUN Bucks branding. Generates HTML, runs structural lint, and captures verification screenshots. Use this skill whenever the user pastes markdown email content, asks to create or generate an email notice/template, references an existing notice in `email-templates/test-inputs/`, or mentions converting a Google Doc draft into HTML. Currently DC-only — Colorado and other states will need a separate skill or a token-driven extension before this can be reused.
allowed-tools: Read, Write, Bash(node email-templates/verify.mjs*), Bash(mkdir -p email-templates/_snapshots), Bash(ls email-templates*), Bash(rm email-templates/_snapshots/*), mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_close
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

1. **Receive markdown content** — If the user references an existing notice by name (e.g. "the pre-approval notice"), check `email-templates/test-inputs/` for a matching `.md` file before asking them to paste content. Otherwise ask the user to paste the markdown if not already provided in the conversation.
2. **Read the reference implementation** — Read `email-templates/PreApprovalNotice.html` first. Email HTML is finicky: MSO conditionals for Outlook, table-based layout, inline-only CSS, and exact entity encoding all matter, and small drift from the reference can break rendering in Outlook or Gmail. The reference contains patterns that have already been validated against real clients, so use it as the canonical source for every structural decision.
3. **Identify template variables** — Scan for placeholder text like `<FIRST NAME>`, `<ISSUE DATE>`, etc. Convert these to `{{CamelCase}}` Handlebars-style variables.
4. **Generate HTML** — Convert the markdown to email-safe HTML following the patterns below exactly.
5. **Write the file** — Save to `email-templates/<TemplateName>.html`. If the template embeds long base64 content (e.g. an inline logo) rather than a runtime `{{LogoHtml}}` placeholder, write a placeholder token (`__LOGO_INLINE__`) here — do NOT paste the base64 directly. See "Injecting inline base64 content" below.
6. **Substitute inline assets** — If you used a placeholder token in step 5, run the script-based substitution now (see "Injecting inline base64 content"). Skip if the template keeps `{{LogoHtml}}` for runtime substitution.
7. **Run structural lint** — Execute `node email-templates/verify.mjs email-templates/<TemplateName>.html`. Always run lint AFTER asset substitution so the `logo-base64-integrity` check (#19) runs against final content. If it fails, fix the issues and re-run.
8. **Capture screenshots** — Use Playwright to render the HTML at 375px (mobile) and 600px (desktop) widths and save screenshots for visual review.
9. **Present for review** — Show the user the screenshots and ask for approval.

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

Each bullet is a table row with two cells. Uses main body font size (16px) and text color (#1b1b1b):

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

Same structure but with legal section styling — smaller font (12px) and muted color (#5c5c5c):

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

### Injecting inline base64 content (logos, etc.)

When a template needs to embed a base64 image directly (rather than relying on `{{LogoHtml}}` runtime substitution), DO NOT paste the base64 string into the Write tool — it will silently corrupt for blobs >10KB. Truncation, duplication, and subtle byte-level transcription errors all happen, and the structural lint can't catch them. Use a script-based substitution instead:

1. Keep the canonical image as a complete `<img>` snippet at a known asset path: `email-templates/assets/sun-bucks-logo-inline.html`.
2. In the HTML you Write, use a placeholder token where the image goes — either `{{LogoHtml}}` (if the renderer will substitute at send-time) or `__LOGO_INLINE__` (if you intend to inline the bytes now).
3. After writing the HTML, substitute via Bash + Node:
   ```bash
   node -e "
   const fs = require('fs');
   const logo = fs.readFileSync('email-templates/assets/sun-bucks-logo-inline.html', 'utf8').trim();
   const path = 'email-templates/<TemplateName>.html';
   fs.writeFileSync(path, fs.readFileSync(path, 'utf8').replace('__LOGO_INLINE__', logo));
   "
   ```
4. Run lint AFTER substitution. The `logo-base64-integrity` check (#19 in `verify.mjs`) compares any embedded `data:image/png;base64,...` URI to the canonical asset byte-for-byte and catches corruption that visual review would otherwise have to find.

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

- **Reference implementation is truth:** When in doubt about any pattern, read `email-templates/PreApprovalNotice.html` and match it. The reference has been validated against real email clients; reinventing patterns risks reintroducing bugs that have already been fixed there.
- **Apply patterns mechanically:** Don't invent new element styles, spacing values, or color combinations. Government notices are brand-locked and legally reviewed — visual consistency across notices is the whole point of having a template skill, and improvisation defeats it.
- **Content from markdown, structure from patterns:** All text content comes from the user's markdown input. All HTML structure and styling comes from the patterns defined above.
- **Legal content varies per notice:** Don't copy legal text from the reference implementation. Each notice has its own legally reviewed language — use what the markdown provides, even if it looks similar to the reference.
- **Lint must pass:** Don't present a template to the user that fails `verify.mjs`. The lint catches things invisible to the eye (unapproved colors, missing entities, `<div>` layout drift) that would otherwise ship to thousands of recipients.
- **Preserve all content:** Don't omit, summarize, or rephrase any content from the markdown input. Government notices have legally reviewed language and dropping a clause can have real legal consequences.
- **HTML entities for special characters:** Use `&ldquo;` `&rdquo;` for smart quotes, `&ndash;` for en-dash, `&#8226;` for bullets, `&rsquo;` for apostrophes. Use numeric HTML entities for non-Latin scripts (Amharic, Korean, Chinese, Vietnamese). Raw Unicode can corrupt in some legacy email clients; entities render reliably everywhere.
- **No `<div>` elements:** Use `<table>` for all layout. Outlook on Windows still uses Word's rendering engine and ignores most modern CSS — table-based layout is the only thing that renders consistently across Outlook, Gmail, Apple Mail, and webmail clients.
- **Section-aware list styling:** Bullet and numbered lists in the main body use 16px/#1b1b1b. Lists in legal sections use 12px/#5c5c5c. The size/color difference is what visually separates "stuff the recipient needs to act on" from "regulatory boilerplate" — mixing them up makes the email look broken.
