#!/usr/bin/env node
// Fetches merged PRs — either from the past N days (--days, default 7) or from an
// exact commit range (--since-sha, diffed up to HEAD) — and writes a release-notes-
// style markdown summary bucketed by state to scripts/release-notes/output/YYYY-MM-DD.md.
//
// Requires the `gh` CLI to be installed and authenticated (`gh auth login`).
// Run via: pnpm release-notes:generate

import { execSync } from 'node:child_process'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

interface PullRequest {
  number: number
  title: string
  labels: { name: string }[]
  mergedAt: string
  mergeCommit: { oid: string } | null
  author: { login: string }
  url: string
}

interface TicketRef {
  ref: string
  jiraUrl: string
}

function parseFlag(argv: string[], name: string): string | undefined {
  const flag = argv.find((a) => a.startsWith(`--${name}=`))
  return flag ? flag.slice(`--${name}=`.length) : undefined
}

function parseDaysArg(raw: string | undefined): number {
  if (!raw) return 7
  const n = parseInt(raw, 10)
  if (isNaN(n) || n < 1) throw new Error(`Invalid --days value: ${raw}`)
  return n
}

// Labels auto-applied from branch prefixes by the PR labeler — reliable for bucketing.
const CHORE_LABELS = new Set([
  'chore',
  'dependabot',
  'infrastructure',
  'security',
  'documentation',
  'refactor',
])

function isChore(pr: PullRequest): boolean {
  return pr.labels.some((l) => CHORE_LABELS.has(l.name.toLowerCase()))
}

// Checks label which we enforce in a pre merge PR check
function isColorado(pr: PullRequest): boolean {
  return pr.labels.some((l) => l.name.toLowerCase() === 'co')
}

// Checks label which we enforce in a pre merge PR check
function isDC(pr: PullRequest): boolean {
  return pr.labels.some((l) => l.name.toLowerCase() === 'dc')
}

function extractTicketRef(title: string): TicketRef | null {
  const match = title.match(/DC-\d+/)
  if (!match) return null
  return {
    ref: match[0],
    jiraUrl: `https://codeforamerica.atlassian.net/browse/${match[0]}`,
  }
}

function formatEntry(pr: PullRequest): string {
  const ticket = extractTicketRef(pr.title)
  // Strip the raw ticket reference from the title so it isn't shown twice.
  const cleanTitle = ticket ? pr.title.replace(/\[?DC-\d+\]?:?[-\s]*/i, '').trim() : pr.title
  const ticketPrefix = ticket ? `[${ticket.ref}](${ticket.jiraUrl}) ` : ''
  return `* ${ticketPrefix}${cleanTitle} by @${pr.author.login} in ${pr.url}`
}

function getPreviousWeeklyTag(): string | null {
  try {
    const raw = execSync('gh release list --json tagName --limit 20', { encoding: 'utf8' })
    const releases = JSON.parse(raw) as { tagName: string }[]
    return releases.find((r) => r.tagName.startsWith('weekly-'))?.tagName ?? null
  } catch {
    return null
  }
}

function getRepoUrl(repoArg: string | undefined): string {
  if (repoArg) return `https://github.com/${repoArg}`
  return execSync('gh repo view --json url -q .url', { encoding: 'utf8' }).trim()
}

// Resolves sinceSha to a commit and returns every commit reached between it and HEAD
// (exclusive of sinceSha itself) — exact, unlike a date window. gitDir is wherever the
// relevant repo (portal or dc-connector) is checked out. sinceSha is expected to
// already be a full, resolvable commit SHA (see resolve-live-sha.sh); this fails
// loudly rather than guessing if it isn't.
function getCommitsSince(gitDir: string, sinceSha: string): Set<string> {
  try {
    execSync(`git -C "${gitDir}" rev-parse --verify "${sinceSha}^{commit}"`, { stdio: 'pipe' })
  } catch {
    throw new Error(`--since-sha ${sinceSha} is not a resolvable commit in ${gitDir}`)
  }
  const raw = execSync(`git -C "${gitDir}" log --format=%H "${sinceSha}..HEAD"`, {
    encoding: 'utf8',
  })
  return new Set(raw.split('\n').filter(Boolean))
}

function buildMarkdown(
  mergedPRs: PullRequest[],
  rangeLabel: string,
  compareUrl: string | null,
  stateFilter: 'dc' | 'co' | null,
): string {
  const co: string[] = []
  const dc: string[] = []
  const both: string[] = []
  const chores: string[] = []

  for (const pr of mergedPRs) {
    const entry = formatEntry(pr)
    if (isChore(pr)) {
      chores.push(entry)
    } else if (isColorado(pr) && !isDC(pr)) {
      co.push(entry)
    } else if (isDC(pr) && !isColorado(pr)) {
      dc.push(entry)
    } else {
      both.push(entry)
    }
  }

  let md = `## What's Changed\n`

  if (mergedPRs.length === 0) {
    md += `\n_No pull requests were merged ${rangeLabel}._\n`
    return md
  }

  // A formal per-state release (--state-filter set) only shows that state's PRs plus
  // generic/Both ones — the other state's PRs and Chores are deliberately left out.
  // Nightly/weekly (no --state-filter) keep showing every bucket, unchanged.
  if (stateFilter !== 'dc' && co.length > 0) {
    md += `\n## CO\n${co.join('\n')}\n`
  }
  if (stateFilter !== 'co' && dc.length > 0) {
    md += `\n## DC\n${dc.join('\n')}\n`
  }
  if (both.length > 0) {
    md += `\n## Both\n${both.join('\n')}\n`
  }
  if (!stateFilter && chores.length > 0) {
    md += `\n## Chores\n${chores.join('\n')}\n`
  }

  if (compareUrl) {
    md += `\n**Full Changelog**: ${compareUrl}\n`
  }

  return md
}

async function main(): Promise<void> {
  const argv = process.argv.slice(2)
  const repoArg = parseFlag(argv, 'repo')
  const sinceSha = parseFlag(argv, 'since-sha')
  const gitDir = parseFlag(argv, 'git-dir') ?? '.'
  const daysArgRaw = parseFlag(argv, 'days')
  const stateFilterArg = parseFlag(argv, 'state-filter')

  if (sinceSha && daysArgRaw) {
    throw new Error('--since-sha and --days are mutually exclusive')
  }
  if (stateFilterArg && stateFilterArg !== 'dc' && stateFilterArg !== 'co') {
    throw new Error(`Invalid --state-filter value: ${stateFilterArg} (expected "dc" or "co")`)
  }
  const stateFilter = (stateFilterArg as 'dc' | 'co' | undefined) ?? null

  const repoFlag = repoArg ? ` --repo ${repoArg}` : ''
  // A limit of 100 (this script's original default) silently truncates once a range
  // spans more than 100 merged PRs — a near-certainty for --since-sha given DC/CO's
  // irregular, sometimes months-long gaps between deploys (confirmed while testing:
  // a 112-commit range needed --limit 500 to avoid dropping 12 real PRs). 1000 is a
  // generous ceiling for a still-fast query; the warning below catches anything that
  // still hits it, rather than silently under-reporting.
  const PR_FETCH_LIMIT = 1000
  // gh resolves owner/repo from the current git remote when --repo isn't given.
  const raw = execSync(
    `gh pr list --state merged --json number,title,labels,mergedAt,mergeCommit,author,url --limit ${PR_FETCH_LIMIT}${repoFlag}`,
    { encoding: 'utf8' },
  )
  const allPRs: PullRequest[] = JSON.parse(raw)
  if (allPRs.length === PR_FETCH_LIMIT) {
    console.warn(
      `::warning::Fetched exactly ${PR_FETCH_LIMIT} merged PRs (the fetch limit) — ` +
        `older PRs may have been silently dropped. Consider raising PR_FETCH_LIMIT.`,
    )
  }

  const repoUrl = getRepoUrl(repoArg)
  const today = new Date().toISOString().split('T')[0]

  let mergedPRs: PullRequest[]
  let rangeLabel: string
  let compareUrl: string | null

  if (sinceSha) {
    const inRange = getCommitsSince(gitDir, sinceSha)
    mergedPRs = allPRs.filter((pr) => pr.mergeCommit && inRange.has(pr.mergeCommit.oid))
    const headSha = execSync(`git -C "${gitDir}" rev-parse HEAD`, { encoding: 'utf8' }).trim()
    rangeLabel = `since ${sinceSha}`
    compareUrl = `${repoUrl}/compare/${sinceSha}...${headSha}`
  } else {
    const days = parseDaysArg(daysArgRaw)
    const since = new Date()
    since.setDate(since.getDate() - days)
    const weekStart = since.toISOString().split('T')[0]
    mergedPRs = allPRs.filter((pr) => pr.mergedAt && new Date(pr.mergedAt) >= since)
    rangeLabel = `between ${weekStart} and ${today}`
    const prevTag = getPreviousWeeklyTag() ?? `weekly-${weekStart}`
    compareUrl = `${repoUrl}/compare/${prevTag}...weekly-${today}`
  }

  const md = buildMarkdown(mergedPRs, rangeLabel, compareUrl, stateFilter)

  // scripts/release-notes/generate.ts → up 3 levels → repo root
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..')
  const outDir = resolve(repoRoot, 'scripts/release-notes/output')
  mkdirSync(outDir, { recursive: true })

  const outPath = resolve(outDir, `${today}.md`)
  writeFileSync(outPath, md, 'utf8')

  console.log(`Written to: scripts/release-notes/output/${today}.md`)
  console.log(`Covered ${mergedPRs.length} merged PR(s) ${rangeLabel}.`)
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})
