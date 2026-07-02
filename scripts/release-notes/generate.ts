#!/usr/bin/env node
// Fetches merged PRs from the past week (or --days=N) and writes a release-notes-style
// markdown summary bucketed by state to scripts/release-notes/output/YYYY-MM-DD.md.
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
  author: { login: string }
  url: string
}

interface TicketRef {
  ref: string
  jiraUrl: string
}

function parseDaysArg(argv: string[]): number {
  const flag = argv.find((a) => a.startsWith('--days='))
  if (!flag) return 7
  const n = parseInt(flag.split('=')[1], 10)
  if (isNaN(n) || n < 1) throw new Error(`Invalid --days value: ${flag.split('=')[1]}`)
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

// Label-first; falls back to word-boundary title match.
// \bCO\b matches standalone "CO" but not "Consolidate", "Connect", etc.
function isColorado(pr: PullRequest): boolean {
  if (pr.labels.some((l) => l.name.toLowerCase() === 'co')) return true
  return /\bCO\b/.test(pr.title) || /colorado/i.test(pr.title)
}

// Label-only — "DC-NNN" ticket prefixes in titles don't mean DC-only.
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
  const raw = execSync('gh release list --json tagName --limit 20', { encoding: 'utf8' })
  const releases = JSON.parse(raw) as { tagName: string }[]
  return releases.find((r) => r.tagName.startsWith('weekly-'))?.tagName ?? null
}

function buildMarkdown(
  mergedPRs: PullRequest[],
  weekStart: string,
  today: string,
  repoUrl: string,
  prevTag: string | null,
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
    md += `\n_No pull requests were merged between ${weekStart} and ${today}._\n`
    return md
  }

  if (co.length > 0) {
    md += `\n## CO\n${co.join('\n')}\n`
  }
  if (dc.length > 0) {
    md += `\n## DC\n${dc.join('\n')}\n`
  }
  if (both.length > 0) {
    md += `\n## Both\n${both.join('\n')}\n`
  }
  if (chores.length > 0) {
    md += `\n## Chores\n${chores.join('\n')}\n`
  }

  if (prevTag && repoUrl) {
    md += `\n**Full Changelog**: ${repoUrl}/compare/${prevTag}...weekly-${today}\n`
  }

  return md
}

async function main(): Promise<void> {
  const days = parseDaysArg(process.argv.slice(2))

  const since = new Date()
  since.setDate(since.getDate() - days)

  const today = new Date().toISOString().split('T')[0]
  const weekStart = since.toISOString().split('T')[0]

  // gh resolves owner/repo from the current git remote and handles auth automatically.
  const raw = execSync(
    `gh pr list --state merged --json number,title,labels,mergedAt,author,url --limit 100`,
    { encoding: 'utf8' },
  )

  const allPRs: PullRequest[] = JSON.parse(raw)
  const mergedPRs = allPRs.filter((pr) => pr.mergedAt && new Date(pr.mergedAt) >= since)

  const repoUrl = mergedPRs.length > 0 ? mergedPRs[0].url.replace(/\/pull\/\d+$/, '') : ''
  const prevTag = getPreviousWeeklyTag()

  const md = buildMarkdown(mergedPRs, weekStart, today, repoUrl, prevTag)

  // scripts/release-notes/generate.ts → up 3 levels → repo root
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..')
  const outDir = resolve(repoRoot, 'scripts/release-notes/output')
  mkdirSync(outDir, { recursive: true })

  const outPath = resolve(outDir, `${today}.md`)
  writeFileSync(outPath, md, 'utf8')

  console.log(`Written to: scripts/release-notes/output/${today}.md`)
  console.log(`Covered ${mergedPRs.length} merged PR(s) from ${weekStart} through ${today}.`)
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})
