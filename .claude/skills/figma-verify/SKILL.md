---
name: figma-verify
description: Compare running app screens against Figma designs — captures screenshots from both sources and produces a structured gap report
allowed-tools: mcp__plugin_figma_figma__get_screenshot, mcp__plugin_figma_figma__get_design_context, mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_snapshot, mcp__plugin_playwright_playwright__browser_close, mcp__plugin_playwright_playwright__browser_click, mcp__plugin_playwright_playwright__browser_fill_form, mcp__plugin_playwright_playwright__browser_select_option, mcp__plugin_playwright_playwright__browser_type, mcp__plugin_playwright_playwright__browser_wait_for, Read, Bash(mkdir:*), Bash(ls:*), Bash(rm:*)
argument-hint: <figma-file-key> <app-base-url> <screen-mappings...>
---

# Figma Design Verification

Compare a running application against Figma designs and produce a structured gap report.

## Overview

This skill captures screenshots of both the Figma designs and the live app, then performs a visual comparison to identify layout, content, styling, and asset gaps. The output is a markdown report with actionable items.

All screenshots are saved to `.claude-figma-verify/` (gitignored) so the user can review them side-by-side after the report.

## Arguments

Parse `$ARGUMENTS` as positional values:

| Position | Name | Example | Required |
|----------|------|---------|----------|
| 1 | Figma file key | `32kDQ73MSbUNdAsVi8fQzF` | Yes |
| 2 | App base URL | `http://localhost:3099` | Yes |
| 3+ | Screen mappings | `landing=6034:16454=/` | Yes (at least one) |

**Screen mapping format:** `<name>=<figma-node-id>=<app-route>`

Example invocation:
```
/figma-verify 32kDQ73MSbUNdAsVi8fQzF http://localhost:3099 landing=6034:16454=/ disclaimer=6736:19904=/disclaimer personalInfo=6034:16775=/check review=8028:29275=/review
```

If arguments are missing or malformed, ask the user to provide them.

## Execution Steps

### 1. Parse and Validate

Extract the file key, base URL, and screen mappings from `$ARGUMENTS`. For each mapping, parse into:
- `name` — human-readable screen name
- `nodeId` — Figma node ID (colon-separated, e.g. `6034:16454`)
- `route` — app route path (e.g. `/disclaimer`)

Validate that at least one screen mapping is provided.

### 2. Set Up Screenshot Directory

```bash
mkdir -p .claude-figma-verify/figma .claude-figma-verify/app
```

This directory is gitignored. Clean up any previous run's files:
```bash
rm -f .claude-figma-verify/figma/*.png .claude-figma-verify/app/*.png
```

### 3. Set Browser Viewport

Resize the Playwright browser to mobile width to match the Figma mobile designs:

- **Width:** 375
- **Height:** 812

### 4. Capture Figma Screenshots (Prototype URL Pass)

Save Figma design screenshots to disk using Figma's prototype presentation view in Playwright. This renders the design frame cleanly with minimal UI chrome.

**For each screen mapping:**

1. Navigate Playwright to the prototype URL:
   ```
   https://www.figma.com/proto/{fileKey}/?node-id={nodeId}&scaling=min-zoom
   ```
   Replace `:` with `-` in the node ID for the URL (e.g., `6034:16454` → `6034-16454`).

2. Wait 5 seconds for the canvas to render (Figma uses WebGL):
   ```
   browser_wait_for(time: 5)
   ```

3. Take a viewport screenshot (NOT full-page — the prototype view fits the frame to the viewport):
   ```
   browser_take_screenshot(filename: ".claude-figma-verify/figma/<name>.png")
   ```

4. After saving to disk, also call `mcp__plugin_figma_figma__get_screenshot` with the file key and node ID. This gives you a higher-fidelity inline image for the visual comparison analysis — the MCP renders at native Figma resolution without browser chrome. Use this inline image as the primary source for your gap analysis.

**IMPORTANT:** The Figma MCP is READ-ONLY. Never use write/modify tools.

**Note:** The prototype URL requires the Figma file to have link sharing enabled. If the page fails to load or shows a login wall, note it in the report and fall back to MCP-only screenshots (inline comparison without disk files).

### 5. Navigate the Full Flow and Capture App Screenshots

**Do NOT navigate directly to each route.** Many screens depend on application state (e.g., the review page needs children in context). Instead, navigate the app as a user would, following the screen mappings in order:

1. Start at the first screen's route (typically `/`)
2. Capture the screenshot for that screen
3. Interact with the app to advance to the next screen (click buttons, fill forms as needed)
4. Capture each subsequent screen after reaching it through the flow

**Form filling strategy:**
- Use realistic but fake data (e.g., "Jane", "M", "Doe", birthdate January 15 2015)
- Fill all required fields before advancing
- If multiple children are needed (e.g., review page shows 3 cards), add multiple children through the form flow before capturing the review screen

**Capturing screenshots:**
Use `mcp__plugin_playwright_playwright__browser_take_screenshot` with:
- `fullPage: true`
- `filename: .claude-figma-verify/app/<name>.png`

**If a screen cannot be reached through the flow** (e.g., a closed/error state), note it in the report as "requires manual setup" and capture what's available.

### 6. Visual Comparison

For each screen, compare the Figma screenshot (inline MCP image) against the app screenshot. Evaluate these dimensions:

| Dimension | What to Check |
|-----------|--------------|
| **Layout** | Element order, spacing, alignment, grid structure |
| **Typography** | Headings, body text size/weight, hierarchy |
| **Colors** | Background, text, button, accent colors |
| **Components** | Buttons, inputs, accordions, cards — do they match USWDS patterns shown in Figma? |
| **Assets** | Logos, icons, illustrations — present and correctly sized? |
| **Content** | Text content matches (accounting for i18n placeholder text in app) |
| **Responsive** | Does the mobile layout match the Figma mobile frame? |

### 7. Generate Report

Produce a structured markdown report. Format:

```markdown
## Figma Verification Report

**Figma file:** `<file-key>`
**App URL:** `<base-url>`
**Viewport:** 375 × 812
**Date:** YYYY-MM-DD
**Screenshots:** `.claude-figma-verify/` (figma/ and app/ subdirectories)

---

### Summary

| Screen | Status | Gaps |
|--------|--------|------|
| Landing | ✅ Match | 0 |
| Disclaimer | ⚠️ Minor gaps | 2 |
| Personal Info | ❌ Major gaps | 4 |

---

### Screen: <Name>

**Figma:** `.claude-figma-verify/figma/<name>.png`
**App:** `.claude-figma-verify/app/<name>.png`

**Status:** ✅ Match | ⚠️ Minor gaps | ❌ Major gaps

**Matches:**
- [list what matches well]

**Gaps:**

| # | Dimension | Severity | Description | Suggestion |
|---|-----------|----------|-------------|------------|
| 1 | Assets | 🔴 Major | Missing logo above heading | Add `<Image>` with logo from Figma |
| 2 | Spacing | 🟡 Minor | Extra margin below button | Adjust `margin-bottom` class |

---
```

#### Severity Levels
- 🔴 **Major** — Missing elements, wrong layout structure, broken functionality
- 🟡 **Minor** — Spacing differences, slight color mismatches, font weight variations
- 🟢 **Trivial** — Sub-pixel differences, animation timing, hover states

### 8. Cleanup

Close the Playwright browser when done.

## Rules

- **READ-ONLY Figma:** Never use Figma write/modify MCP tools. Only `get_screenshot` and `get_design_context`.
- **No automatic fixes:** This skill only reports gaps. Do not modify application code. If the user wants fixes, they will ask separately.
- **i18n tolerance:** The app may show placeholder text or translation keys. Note these as content gaps only if the Figma design shows specific text that doesn't appear at all.
- **USWDS tolerance:** Minor rendering differences between USWDS components and Figma mockups are expected. Only flag structural differences (wrong component type, missing component).
- **Always show both screenshots:** The report should reference both the Figma and app screenshots so the user can visually confirm findings.
- **Full-page screenshots:** Always use `fullPage: true` for app screenshots to capture below-the-fold content.
- **Save all screenshots to disk:** Both Figma and app screenshots must be saved to `.claude-figma-verify/` so the user can review them after the session.
- **Navigate the flow, don't jump:** Always reach screens through the normal app flow to ensure correct application state. Only use direct navigation for the starting screen.

## Edge Cases

- **App not running:** If navigation fails, report the error and skip that screen. Continue with remaining screens.
- **Figma node not found:** If the Figma screenshot fails, report it and skip that screen.
- **Figma login wall:** If the prototype URL redirects to a login page instead of rendering the design, fall back to MCP-only inline screenshots. Note in the report that Figma screenshots could not be saved to disk because the file requires authentication. The user may need to enable link sharing on the Figma file.
- **Interactive states:** If a screen requires interaction beyond simple form filling (e.g., API responses, error states), note it as "requires manual setup" and describe what state is needed.
- **No arguments provided:** Show the usage format and example invocation, then ask the user to provide arguments.
