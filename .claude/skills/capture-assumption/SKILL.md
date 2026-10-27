---
name: capture-assumption
description: Propose entries for the project assumptions registry (docs/assumptions.md). Use when a broad assumption about state backends, connectors, config, deployment, or business rules is stated, questioned, or contradicted in a session or PR discussion — or to harvest assumptions from a PR's review threads.
allowed-tools: Read, Grep, Bash(gh pr view:*), Bash(gh api:*), Bash(git log:*), Bash(git diff:*)
argument-hint: [PR number | free-text assumption] (default: current conversation)
---

# Capture Assumption

Turn tribal knowledge into entries for `docs/assumptions.md`, the registry Claude consults
during coding sessions and reviews.

**Claude proposes; a human writes.** Per CLAUDE.md's prose-documentation rule, never edit
`docs/assumptions.md` directly. Output proposed entries in chat for a human to add.

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

Read `docs/assumptions.md`. If it doesn't exist yet, say so and still output proposals.

- **Duplicate** → don't re-propose; note the existing entry.
- **Contradicts a confirmed entry** → flag prominently. Either the code or the registry is wrong; a human decides which.
- **Refines an existing entry** → propose an edit to that entry rather than a new one.

## 4. Output proposals

One block per assumption, using exactly these fields:

```markdown
- **Assumption:** <one sentence, stated as a testable fact>
  - **Scope:** <states / components affected>
  - **Source:** <PR #, Jira ticket, or conversation; person by role, never by name>
  - **Status:** confirmed | unverified | disputed
  - **Last verified:** <YYYY-MM-DD, or "never">
```

Rules:

- Default **Status** to `unverified` unless a source with authority (state agency, connector code, ticket) confirms it.
- **Source** must be traceable. "Everyone knows" is not a source — mark it `unverified` and say who could confirm it.
- No PII, secrets, or real household data in any field.

End with a one-line summary: N proposed, N duplicates, N contradictions, N routed elsewhere.
