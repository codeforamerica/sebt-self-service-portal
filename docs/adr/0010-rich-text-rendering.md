# 10. Rich text rendering with markdown-to-jsx

Date: 2026-03-16

## Status

Accepted

## Context

Locale strings from state CSVs contain markdown-style formatting (`**bold**`, `\n` paragraph breaks). The project needs a way to render these as styled React elements safely. Currently, bold syntax displays as literal asterisks in the UI.

## Decision

Add `markdown-to-jsx` to the design-system package, wrapped in a `RichText` component. Content is rendered at runtime — no transformation of CSVs or generated JSON files. What content authors write in the CSV is what's in the JSON is what the component receives.

Usage:
- Plain text (majority): `{t('key')}` — unchanged
- Inline markdown (bold within a sentence): `<RichText inline>{t('key')}</RichText>`
- Multi-paragraph markdown: `<RichText>{t('key')}</RichText>`

`markdown-to-jsx` produces React elements directly from markdown strings, making it safe by construction — no raw HTML injection is possible.

## Alternatives Considered

1. **Transform markdown to HTML at generation time in `generate-locales.js`**, then use i18next `<Trans>` component — adds a hidden transformation layer, JSON files become less human-readable, and couples the generation script to specific markdown patterns.
2. **`react-markdown`** — full CommonMark renderer with remark/rehype ecosystem (~60KB, 10+ transitive deps). Significantly overpowered for our needs (primarily bold text and paragraph breaks).
3. **Hand-implement a custom markdown-to-React parser** — error-prone, testing burden, maintenance overhead. A well-tested library is the safer choice.

## Consequences

- New `RichText` component available to both portal and enrollment checker.
- Components must opt in by wrapping `t()` calls in `<RichText>`.
- No changes to the content pipeline.
- ~18KB added bundle size (zero transitive dependencies).
