import { strict as assert } from 'node:assert'
import { describe, it } from 'node:test'
import {
  buildMarkdown,
  extractTicketRef,
  formatEntry,
  isChore,
  isColorado,
  isDC,
  type PullRequest,
} from './generate.ts'

function pr(overrides: Partial<PullRequest> = {}): PullRequest {
  const number = overrides.number ?? 1
  return {
    number,
    title: 'Some change',
    labels: [],
    mergedAt: '2026-08-01T00:00:00Z',
    mergeCommit: { oid: 'abc123' },
    author: { login: 'someone' },
    url: `https://github.com/codeforamerica/sebt-self-service-portal/pull/${number}`,
    ...overrides,
  }
}

const label = (name: string) => ({ name })

describe('isChore', () => {
  it('matches a known chore label, case-insensitively', () => {
    assert.equal(isChore(pr({ labels: [label('Chore')] })), true)
    assert.equal(isChore(pr({ labels: [label('dependabot')] })), true)
  })

  it('does not match an unrelated label', () => {
    assert.equal(isChore(pr({ labels: [label('feature')] })), false)
  })

  it('does not match a PR with no labels', () => {
    assert.equal(isChore(pr({ labels: [] })), false)
  })
})

describe('isColorado / isDC', () => {
  it('matches only the exact label, case-insensitively', () => {
    assert.equal(isColorado(pr({ labels: [label('CO')] })), true)
    assert.equal(isDC(pr({ labels: [label('DC')] })), true)
  })

  it('does not fall back to title text — the real bug this was fixed for', () => {
    // Real example found during design: "DC-513(Spike): Local Keycloak OIDC
    // stand-in for CO development" is CO-labeled work whose title happens to
    // contain both "DC-513" (Jira project prefix, unrelated to state) and "CO".
    // A title-based fallback would have misclassified this as DC-relevant.
    const misleadingTitlePr = pr({
      title: 'DC-513(Spike): Local Keycloak OIDC stand-in for CO development',
      labels: [label('co')],
    })
    assert.equal(isColorado(misleadingTitlePr), true)
    assert.equal(isDC(misleadingTitlePr), false)
  })

  it('does not match on an unlabeled PR regardless of title contents', () => {
    const unlabeled = pr({ title: 'Fix the CO and DC copy', labels: [] })
    assert.equal(isColorado(unlabeled), false)
    assert.equal(isDC(unlabeled), false)
  })
})

describe('extractTicketRef', () => {
  it('extracts a DC-NNN ticket reference', () => {
    const ref = extractTicketRef('DC-123 Fix the thing')
    assert.deepEqual(ref, {
      ref: 'DC-123',
      jiraUrl: 'https://codeforamerica.atlassian.net/browse/DC-123',
    })
  })

  it('returns null when no ticket reference is present', () => {
    assert.equal(extractTicketRef('Bump some-package from 1.0.0 to 1.0.1'), null)
  })
})

describe('formatEntry', () => {
  it('strips the ticket reference from the title and links it instead', () => {
    const entry = formatEntry(
      pr({ title: 'DC-123: Fix the thing', author: { login: 'alice' } }),
    )
    assert.equal(
      entry,
      '* [DC-123](https://codeforamerica.atlassian.net/browse/DC-123) Fix the thing by @alice in https://github.com/codeforamerica/sebt-self-service-portal/pull/1',
    )
  })

  it('leaves the title untouched when there is no ticket reference', () => {
    const entry = formatEntry(pr({ title: 'Bump next from 1.0.0 to 1.0.1' }))
    assert.equal(
      entry,
      '* Bump next from 1.0.0 to 1.0.1 by @someone in https://github.com/codeforamerica/sebt-self-service-portal/pull/1',
    )
  })
})

describe('buildMarkdown', () => {
  it('shows a "no pull requests" message for an empty range', () => {
    const md = buildMarkdown([], 'between 2026-08-01 and 2026-08-07', null, null)
    assert.match(md, /No pull requests were merged between 2026-08-01 and 2026-08-07/)
  })

  it('buckets CO-only, DC-only, and portal-wide PRs into separate sections', () => {
    const md = buildMarkdown(
      [
        pr({ number: 1, labels: [label('co')] }),
        pr({ number: 2, labels: [label('dc')] }),
        pr({ number: 3, labels: [] }),
      ],
      'since x',
      null,
      null,
    )
    assert.match(md, /## CO Specific/)
    assert.match(md, /## DC Specific/)
    assert.match(md, /## Portal Wide Changes/)
    assert.match(md, /pull\/1/)
    assert.match(md, /pull\/2/)
    assert.match(md, /pull\/3/)
  })

  it('chore label takes precedence even when a state label is also present', () => {
    const md = buildMarkdown(
      [pr({ number: 1, labels: [label('chore'), label('dc')] })],
      'since x',
      null,
      null,
    )
    assert.match(md, /## Chores/)
    assert.doesNotMatch(md, /## DC Specific/)
  })

  it('--state-filter=dc excludes the CO section and Chores', () => {
    const md = buildMarkdown(
      [
        pr({ number: 1, labels: [label('co')] }),
        pr({ number: 2, labels: [label('dc')] }),
        pr({ number: 3, labels: [label('chore')] }),
      ],
      'since x',
      null,
      'dc',
    )
    assert.doesNotMatch(md, /## CO Specific/)
    assert.doesNotMatch(md, /## Chores/)
    assert.match(md, /## DC Specific/)
  })

  it('--state-filter=co excludes the DC section and Chores', () => {
    const md = buildMarkdown(
      [
        pr({ number: 1, labels: [label('co')] }),
        pr({ number: 2, labels: [label('dc')] }),
        pr({ number: 3, labels: [label('chore')] }),
      ],
      'since x',
      null,
      'co',
    )
    assert.doesNotMatch(md, /## DC Specific/)
    assert.doesNotMatch(md, /## Chores/)
    assert.match(md, /## CO Specific/)
  })

  it('includeChores opts Chores back in even with --state-filter set', () => {
    const md = buildMarkdown(
      [pr({ number: 1, labels: [label('dc')] }), pr({ number: 2, labels: [label('chore')] })],
      'since x',
      null,
      'dc',
      true,
    )
    assert.match(md, /## DC Specific/)
    assert.match(md, /## Chores/)
  })

  it('includeChores has no effect without --state-filter (Chores already shown)', () => {
    const md = buildMarkdown(
      [pr({ number: 1, labels: [label('chore')] })],
      'since x',
      null,
      null,
      true,
    )
    assert.match(md, /## Chores/)
  })

  it('includes the compare link only when one is given', () => {
    const withLink = buildMarkdown([pr()], 'since x', 'https://example.com/compare/a...b', null)
    assert.match(withLink, /\*\*Full Changelog\*\*: https:\/\/example\.com\/compare\/a\.\.\.b/)

    const withoutLink = buildMarkdown([pr()], 'since x', null, null)
    assert.doesNotMatch(withoutLink, /Full Changelog/)
  })
})
