---
name: qa
description: "Generate QA test summary from current branch changes for PR handoff"
allowed-tools: Read, Grep, Glob, Bash, Write
---

# /qa - QA Test Summary Generator

Generate a QA handoff document from the current branch's changes. Output should be ready to paste as a **Jira comment** on the ticket.

**Write two versions of the output:**
1. **`.claude/qa-summary.md`** — Markdown version with proper headings, bold, checkboxes, and code formatting. Useful for GitHub PRs or reference.
2. **`.claude/qa-summary.txt`** — Plain text version following the Jira formatting rules below. This is the one that gets copied to clipboard.

After writing both files, run `cat .claude/qa-summary.txt | pbcopy` to copy the Jira version to the clipboard. Tell the user: "Copied to clipboard — paste into Jira with Cmd+V. Markdown version also saved to `.claude/qa-summary.md`."

## Instructions

1. **Discover changes**: Diff against the branch's parent, not production. Run `git diff development --stat` and `git log development..HEAD --oneline` to see only what this branch adds. Also run `git diff HEAD --stat` to catch any uncommitted work.
2. **Read changed files**: Read the key modified files to understand the actual changes, not just the diff stats
3. **Identify scope**: Determine which features, pages, and components are affected
4. **Generate test summary in plain text with light formatting.** The output will be pasted into Jira Cloud's rich text editor. Use this format:

```
QA Summary — [Branch Name]

What Changed
Brief 2-3 sentence description of the feature/fix.

Affected Areas
- All pages — page views and browser tab titles
- Sign in / Sign out flow

Test Cases

Happy Path
1. Step-by-step test case with expected result
2. Another test case...

Edge Cases
1. Edge case scenario with expected behavior

Regression Checks
1. Verify [related feature] still works as expected

Environment Notes
- Environment setup needed
- Which environments to test on
```

### Formatting rules — CRITICAL
- Use plain text with simple numbered and bulleted lists
- NO markdown syntax (no ##, no **, no backticks, no [ ] checkboxes)
- NO Jira wiki markup (no h2., no *, no {{}}, no #)
- Section headers should be plain text on their own line, in ALL CAPS or Title Case to stand out
- Use dashes (-) for bullet lists and numbers (1. 2. 3.) for ordered lists
- For inline code references like event names, just use quotes: "page_view", "sign_in"
- For links, write the full URL on its own line

## Key Behaviors
- Focus on **user-facing behavior**, not implementation details
- Test cases should be actionable by someone who didn't write the code
- Use relative paths for navigation (e.g. "Navigate to Tools > Tag Generator"), never reference localhost or full URLs — QA knows their environment
- Assume QA is already signed in and on the app unless the test specifically involves auth
- Call out any data dependencies (specific campaigns, creatives, etc.)
- If changes touch auth, API layer, or shared components, expand regression scope
- Keep it concise — QA doesn't need to know about refactors or code style changes
- Use checkboxes only in the markdown version (`qa-summary.md`); the plain-text Jira version uses numbered lists per the formatting rules above

## Language Rules — CRITICAL
- **NEVER reference file names, paths, components, stores, functions, or code concepts.** QA does not read code.
- Write everything in terms of **what the user sees and does in the browser** — pages, buttons, modals, fields, messages.
- Instead of "interceptors.js tracks CRUD operations", say "Creating, editing, or deleting any item should be tracked"
- Instead of "TableFilter.vue search is debounced", say "Type in a search bar, wait a second, and the search should be recorded"
- Instead of "Affected: src/features/analytics/...", say "Affected: Analytics reports page"
- The "Affected Areas" section should list **page names and UI areas**, not files or components
- Environment Notes should only include things QA needs to configure or be aware of — no technical implementation details
