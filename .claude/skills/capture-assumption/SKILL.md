---
name: capture-assumption
description: Add proposed entries to the project assumptions registry (docs/assumptions.md). Use when a broad assumption about state backends, connectors, config, deployment, or business rules is stated, questioned, or contradicted in a session or PR discussion — or to harvest assumptions from a PR's review threads.
allowed-tools: Read, Edit, Grep, Bash(gh pr view:*), Bash(gh api:*), Bash(git log:*), Bash(git diff:*)
argument-hint: [PR number | free-text assumption] (default: current conversation)
---

# Capture Assumption

Turn tribal knowledge into entries in `docs/assumptions.md`, the registry Claude consults
during coding sessions and reviews.

**Claude proposes; humans decide.** Claude adds entries with status `proposed` only. A human
reviews each one and sets `confirmed`, `disputed`, or `rejected`, or corrects the text.
Never decide those statuses yourself. The only time you may change a reviewed entry is to
record a decision a human states explicitly in the session (see step 5).

## 1. Gather candidates

Parse `$ARGUMENTS`:

- **Empty** → scan the current conversation for assumptions that were stated, questioned, or contradicted.
- **PR number** → read the review threads:
  ```bash
  gh pr view <n> --comments
  gh api repos/{owner}/{repo}/pulls/<n>/comments --paginate --jq '.[] | {path, line, user: .user.login, body}'
  ```
- **Free text** → treat it as a single candidate.

Look for phrasing like "we assume", "I think DC always…", "is it safe to rely on…", "CO never…",
"that's how the state does it", or a reviewer asking "how do we know X?".

## 2. Filter for broad assumptions

Keep a candidate only if **all** apply:

- It's a fact about the world the code relies on, not a decision (decisions go in ADRs).
- It spans more than one file or feature.
- It cannot be derived by reading the code.
- A state-agency maintainer inheriting the code would be surprised or harmed by not knowing it.

Route everything else elsewhere and say so:

| Candidate is… | Belongs in |
|---|---|
| Local to one function or file | A short code comment |
| A decision with alternatives considered | An ADR in `docs/adr/` (human-written) |
| A personal preference | User memory, not the registry |

## 3. Check against the registry

Read `docs/assumptions.md`.

- **Duplicate** → skip it; note the existing entry.
- **Previously `rejected`** → don't re-add. Mention it only if there's new evidence.
- **Contradicts a `confirmed` entry** → don't edit the entry. Flag the contradiction in chat; a human decides whether the code or the registry is wrong.
- **Refines an existing `proposed` entry** → edit that entry in place.

## 4. Write entries

Add each entry under the best-fitting `##` area heading (create one if none fits), using exactly:

```markdown
- **Assumption:** <one sentence, stated as a testable fact>
  - **Scope:** <states / components affected>
  - **Source:** <file:line, PR #, or Jira ticket; people by role, never by name>
  - **Status:** proposed
  - **Last verified:** never
  - **Revisit by:** <YYYY-MM-DD>   (optional)
```

Rules:

- **Source** must be traceable to something a reviewer can open. Never add an entry without one.
- If the source itself hedges or calls the point an open question, say so in **Source** (e.g. "flagged as open question in …").
- Add **Revisit by** only when the fact has a known expiry (a program year, a planned rollout, a dated commitment). Never guess a date.
- Dates are always `YYYY-MM-DD`; `.github/workflows/scripts/assumptions-review.js` parses them.
- No PII, secrets, or real household data in any field.

End with a one-line summary in chat: N added, N duplicates skipped, N contradictions flagged, N routed elsewhere.

## 5. Record a human's decision

When a human explicitly confirms, disputes, rejects, or corrects an entry in the session:

- Set **Status** to what they said, and **Last verified** to today.
- Append to **Source** how it was verified, by role (e.g. "confirmed by CO product owner, 2026-10-27").
- If they're unsure, leave the entry as-is and note who could answer.

Never infer a decision from silence, approval of unrelated work, or a merged PR.
