---
allowed-tools: Bash(git:*), Bash(wc:*), Bash(grep:*), Bash(find:*), Bash(head:*), Bash(tail:*), Bash(printf:*), Bash(shasum:*), Read
argument-hint: [repository](default: sebt-self-service-portal) [base-branch|commit-ref] (default: main)
description: Analyze code changes and generate a summary and prioritized human review guide. Use for larger PRs or preparing code for review.
---

# PR Review Priority Analysis

Generate a prioritized review guide for human reviewers. Our analysis will identify which code changes warrant the most human attention.

## Execution Steps

### 1. Determine Diff Source

Parse `$ARGUMENTS` to determine comparison target:
- Empty or "main" → `git diff main...HEAD`
- Branch name → `git diff <branch>...HEAD`  
- Commit ref (SHA, HEAD~N) → `git diff <ref>`
- "staged" → `git diff --cached`
- "unstaged" → `git diff`

Verify the ref exists before proceeding. If invalid, report available branches.

### 2. Gather Change Metrics

```bash
# Get list of changed files with stats
git diff <ref> --stat

# Get detailed diff for analysis
git diff <ref> --unified=3
```

Extract:
- Files added, modified, deleted
- Lines added/removed per file
- Total change volume

### 3. Identify Review Units

For each changed file, identify discrete units requiring review:
- **New functions/methods**: Extract function name and line range
- **Modified functions**: Note what changed
- **Configuration changes**: Flag separately
- **Documentation changes**: identify semantically distinct chunks of docs
- **New files**: Entire file is one unit 


Use language-appropriate patterns:
- Python: `def `, `class `, `async def `
- Ruby: `def`, `class`, `module`
- JavaScript/TypeScript: `function `, `const.*=.*=>`, `class `, `export `
- Go: `func `
- Java or C#: Method signatures, class declarations

### 4. Score Each Unit

Evaluate each review unit against three criteria:

#### Security Risk (Weight: Highest)
🔴 **High**: Authentication, authorization, secrets/credentials, storing personal information, file system access, network requests, and so on
🟡 **Medium**: Configuration, permissions, logging (if potential for PII leaks), session handling, rate limiting
🟢 **Low**: Pure computation, display logic, tests, documentation, styling

#### Complexity (Weight: Medium)
🔴 **High**: Deep cyclomatic complexity, multiple code paths, recursive logic, complex conditionals, concurrent/async patterns, 
🟡 **Medium**: Moderate branching, callbacks, error handling paths
🟢 **Low**: Linear flow or simple conditionals, simple CRUD, straightforward transformations

#### Novelty (Weight: Medium)
🔴 **High**: New libraries, significant departures from existing code
🟡 **Medium**: first-of-kind in codebase or moderate variations on existing patterns
🟢 **Low**: Boilerplate, repeated patterns; stuff that clearly builds on well-known patterns or code that already exists in code base

Also generate an aggregate rating and, for medium or high risk, concisely explain (2-6 words) the reason for attention

### 5. Generate Output

#### Format: Markdown Report

```markdown
## PR Review Priority Guide

**Risk Level**: [🔴 High | 🟡 Medium | 🟢 Low] | **Files**: N | **Lines**: +X / -Y

[1-2 sentence summary: what this PR does and where reviewer attention is needed]

---

### Priority Review Table

| Priority | File | Function/Change | Lines | Sec | Cpx | Nov | Notes |
|:--------:|------|-----------------|:-----:|:---:|:---:|:---:|-------|
| 🔴 | `path/to/file.py` | `authenticate_user()` | 45-89 | 🔴 | 🟡 | 🔴 | New auth flow, handles tokens |
| 🔴 | `api/medicaid.py` | `process_household()` | 112-156 | 🔴 | 🟢 | 🟡 | New data model with sensitive PII |
| 🟡 | `utils/cache.py` | `invalidate_all()` | 23-45 | 🟢 | 🟡 | 🟡 | New cache pattern |
| 🟢 | `models/user.py` | `UserProfile` class | 1-34 | 🟢 | 🟢 | 🟢 | Standard model |

---

### Summary by Priority

**🔴 Critical Review (N items)**
- List critical items with brief rationale

**🟡 Recommended Review (N items)**  
- List medium-priority items

**🟢 Low Priority (N items)**
- Boilerplate, tests, config (can be skimmed)

---

### Review Recommendation

[Actionable guidance: "Start with auth/oauth.py lines 45-89, then review payment logic. Test files can be skimmed for coverage gaps."]
```

## Output Guidelines

- Sort table by priority (🔴 → 🟡 → 🟢), then by file path
- Files should be links, where the link text is a path from the repo root, and the URL will jump directly to the first line of the range in the PR changeset. Use shasum if needed to generate the links.
- Line numbers should be clickable-friendly (exact ranges). 
- Keep notes column concise (under 40 chars)
- For very large PRs (50+ review units), group by directory and show top 20 critical items with a note about remaining
- If no 🔴 items exist, explicitly state "No critical security concerns identified"
- Always include the aggregate risk assessment

## Edge Cases

- **No changes found**: Report "No changes detected against <ref>"
- **Binary files**: Note as "binary file changed" with 🟡 priority
- **Renamed files**: Track as low priority unless content also changed
- **Deleted files**: List separately; generally low priority unless removing security controls
