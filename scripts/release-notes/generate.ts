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

export interface PullRequest {
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

export function isChore(pr: PullRequest): boolean {
  return pr.labels.some((l) => CHORE_LABELS.has(l.name.toLowerCase()))
}

// Checks label which we enforce in a pre merge PR check
export function isColorado(pr: PullRequest): boolean {
  return pr.labels.some((l) => l.name.toLowerCase() === 'co')
}

// Checks label which we enforce in a pre merge PR check
export function isDC(pr: PullRequest): boolean {
  return pr.labels.some((l) => l.name.toLowerCase() === 'dc')
}

export function extractTicketRef(title: string): TicketRef | null {
  const match = title.match(/DC-\d+/)
  if (!match) return null
  return {
    ref: match[0],
    jiraUrl: `https://codeforamerica.atlassian.net/browse/${match[0]}`,
  }
}

export function formatEntry(pr: PullRequest): string {
  const ticket = extractTicketRef(pr.title)
  // Strip the raw ticket reference from the title so it isn't shown twice.
  const cleanTitle = ticket ? pr.title.replace(/\[?DC-\d+\]?:?[-\s]*/i, '').trim() : pr.title
  const ticketPrefix = ticket ? `[${ticket.ref}](${ticket.jiraUrl}) ` : ''
  return `* ${ticketPrefix}${cleanTitle} by @${pr.author.login} in ${pr.url}`
}

interface PreviousRelease {
  tagName: string
  createdAt: string
}

// Finds the most recent release tagged with this cadence's prefix, regardless of
// draft status — weekly releases never get published (confirmed against real data:
// every existing weekly-* release is still a draft, even ones from weeks ago), so
// filtering to published-only would mean weekly never finds a previous run and never
// benefits from "since last run" below. Returns null on the very first run of a given
// cadence (e.g. nightly-* the day this ships), which callers treat as "no prior run
// to diff against yet".
function getPreviousRelease(tagPrefix: string): PreviousRelease | null {
  try {
    const raw = execSync('gh release list --json tagName,createdAt --limit 20', {
      encoding: 'utf8',
    })
    const releases = JSON.parse(raw) as PreviousRelease[]
    return releases.find((r) => r.tagName.startsWith(`${tagPrefix}-`)) ?? null
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

export function buildMarkdown(
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
  // the portal-wide ones — the other state's PRs and Chores are deliberately left out.
  // Nightly/weekly (no --state-filter) keep showing every bucket, unchanged.
  if (stateFilter !== 'dc' && co.length > 0) {
    md += `\n## CO Specific\n${co.join('\n')}\n`
  }
  if (stateFilter !== 'co' && dc.length > 0) {
    md += `\n## DC Specific\n${dc.join('\n')}\n`
  }
  if (both.length > 0) {
    md += `\n## Portal Wide Changes\n${both.join('\n')}\n`
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
  // Only meaningful in date-window mode — generate-release-notes.yml runs both the
  // weekly and nightly cadence through this same script/job, and each cadence tags
  // its own release differently (weekly-YYYY-MM-DD vs nightly-YYYY-MM-DD). Without
  // this, the "Full Changelog" link would always reference a weekly-* tag even on a
  // nightly run, pointing at a tag that was never created.
  const tagPrefix = parseFlag(argv, 'tag-prefix') ?? 'weekly'

  if (sinceSha && daysArgRaw) {
    throw new Error('--since-sha and --days are mutually exclusive')
  }
  if (stateFilterArg && stateFilterArg !== 'dc' && stateFilterArg !== 'co') {
    throw new Error(`Invalid --state-filter value: ${stateFilterArg} (expected "dc" or "co")`)
  }
  const stateFilter = (stateFilterArg as 'dc' | 'co' | undefined) ?? null

  const repoFlag = repoArg ? ` --repo ${repoArg}` : ''
  // A low limit would silently truncate once a range spans more merged PRs than the
  // limit — a real risk for --since-sha given DC/CO's irregular, sometimes
  // months-long gaps between deploys. 1000 is a generous ceiling for a still-fast
  // query; the warning below catches anything that still hits it, rather than
  // silently under-reporting.
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
    // --days is now only the fallback window for a cadence's first-ever run (no
    // previous release of this tag prefix to diff against yet) — otherwise this
    // diffs against the last actual run, so a skipped/failed run doesn't silently
    // drop whatever merged during the gap.
    const days = parseDaysArg(daysArgRaw)
    const fallbackSince = new Date()
    fallbackSince.setDate(fallbackSince.getDate() - days)

    const previousRelease = getPreviousRelease(tagPrefix)
    const since = previousRelease ? new Date(previousRelease.createdAt) : fallbackSince
    const rangeStart = since.toISOString().split('T')[0]

    mergedPRs = allPRs.filter((pr) => pr.mergedAt && new Date(pr.mergedAt) >= since)
    rangeLabel = previousRelease
      ? `since the last ${tagPrefix} release (${previousRelease.tagName})`
      : `between ${rangeStart} and ${today}`

    const prevTagName = previousRelease?.tagName ?? `${tagPrefix}-${rangeStart}`
    compareUrl = `${repoUrl}/compare/${prevTagName}...${tagPrefix}-${today}`
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
