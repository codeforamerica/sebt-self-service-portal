# i18n Integrity Audit

## What this is

A build-time gate that catches the failure modes the CSV → JSON pipeline cannot:

1. **`used-but-missing`** — code calls `t('foo')` but no row in the sheet defines `foo`
2. **`used-but-empty`** — code calls `t('foo')`, the sheet has the row, but the cell is blank
3. **`fallback-masked`** — same as 1/2, but the call site has a string fallback (`t('foo', 'Hello')`); rendered text is fine, but the sheet is still incomplete
4. **`orphan`** — sheet defines `foo` but no `t('foo')` call ever references it

Run via `pnpm lint:i18n` from `src/SEBT.Portal.Web/`. Fires in three places:

- **Pre-commit** (`.husky/pre-commit`) — runs on commits that touch `src/SEBT.Portal.Web/` or `packages/design-system/content/`. Fast feedback before a bad commit lands locally.
- **Prebuild** (`pnpm build` → `prebuild`) — CI safety net. Builds (production, deployments) fail on a regression.
- **Manual** (`pnpm lint:i18n`) — ad-hoc.

## The baseline ratchet

The repo started with **97 unmasked errors** (call sites with no fallback hitting missing/empty JSON), **550 masked errors** (fallbacks hiding incomplete copy), and **353 orphan keys**. Listing the unmasked ones in `docs/i18n-audit-baseline.json` makes the gate enforceable today: existing debt is grandfathered, *new* regressions fail.

Update the baseline only after deliberate cleanup work — every entry shrunk is a net-good, every entry added is debt accepted on purpose:

```sh
# After fixing some entries in the sheet and re-running pnpm copy:generate:
cd src/SEBT.Portal.Web
pnpm lint:i18n:update-baseline   # writes the new (smaller) baseline
git add ../../docs/i18n-audit-baseline.json
```

The schema is intentionally tiny so PR diffs against the baseline read like a release note: each removed entry = one bug fixed.

## Cleanup priority

Treat the four categories as a queue:

1. **Unmasked errors first.** These render empty or fallback-substituted text to users *right now*. The audit lists each by `file:line` so the content owner can find the call site, look up the missing string, and add the row to the sheet.
2. **Masked errors next.** Users see the engineer's hardcoded fallback string instead of the curated copy from the sheet. Each one is content drift — fix the sheet, then drop the fallback.
3. **Orphans last.** Sheet rows nothing renders. Either delete (most common) or wire up a `t()` call (rare — usually means a feature was abandoned).

## What the gate does NOT catch

- Hardcoded JSX text (`<p>Hello</p>` with no `t()` at all). That's a separate ESLint rule (`eslint-plugin-i18next/no-literal-string`) — tracked as a follow-up.
- Backend strings (C# `BadRequest("Email required")`) that surface to the UI. Different problem space.
- Dynamically constructed keys (`` t(`prefix.${name}`) ``). The audit silently skips these; if you write a dynamic key, it's on you to ensure the sheet covers every possible value.
- Plural / interpolation correctness (`t('foo', { count: 1 })`). The audit only checks key existence and emptiness, not pluralization.

## Adding a new key

1. Add the key + value to the Google Sheet (per state, per locale).
2. Re-export the CSV → commit to `packages/design-system/content/states/{state}.csv`.
3. `pnpm copy:generate` regenerates the JSON.
4. Use `t('key')` in code. The audit will pass.

If step 1 hasn't happened yet and you ship `t('key')` in code, the audit fails the build. Use a fallback (`t('key', 'Temporary')`) if you need to merge before the sheet update — the audit downgrades that to a warning so it doesn't block, but it's logged as debt that someone (you, ideally) needs to circle back on.
