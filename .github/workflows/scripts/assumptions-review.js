// Opens (or updates) a single issue listing assumptions-registry entries that need a
// human: past their revisit date, disputed, confirmed but stale, or still proposed.
// Closes the issue once nothing needs review. Uses a hidden marker to find the same
// issue across runs, like require-state-label.js does for its PR comment.
const fs = require("fs");
const path = require("path");

const REGISTRY_PATH = "docs/assumptions.md";
const MARKER = "<!-- assumptions-review -->";
const ISSUE_TITLE = "Assumptions registry: entries need human review";
const STALE_AFTER_DAYS = 180;
const DAY_MS = 24 * 60 * 60 * 1000;

// An entry appears once, under the first group it qualifies for.
const GROUPS = [
  { key: "pastRevisit", heading: "⏰ Past their revisit date" },
  { key: "disputed", heading: "⚠️ Disputed" },
  { key: "stale", heading: `🕰️ Confirmed, but not verified in ${STALE_AFTER_DAYS}+ days` },
  { key: "proposed", heading: "📝 Proposed, awaiting review" },
];

function parseEntries(markdown) {
  const entries = [];
  let current = null;

  markdown.split("\n").forEach((line, index) => {
    const assumption = line.match(/^- \*\*Assumption:\*\* (.+)$/);
    if (assumption) {
      current = { assumption: assumption[1].trim(), line: index + 1, fields: {} };
      entries.push(current);
      return;
    }

    const field = line.match(/^\s+- \*\*(.+?):\*\* (.+)$/);
    if (current && field) {
      current.fields[field[1].trim().toLowerCase()] = field[2].trim();
    }
  });

  return entries;
}

function parseDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value ?? "")) {
    return null;
  }
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function classify(entry, today) {
  const status = (entry.fields.status ?? "").toLowerCase();
  if (status === "rejected") {
    return null;
  }

  const revisitBy = parseDate(entry.fields["revisit by"]);
  if (revisitBy && revisitBy <= today) {
    return "pastRevisit";
  }
  if (status === "disputed") {
    return "disputed";
  }
  if (status === "confirmed") {
    // A confirmed entry with a missing or malformed date counts as never verified.
    const lastVerified = parseDate(entry.fields["last verified"]);
    const isStale = !lastVerified || today - lastVerified > STALE_AFTER_DAYS * DAY_MS;
    return isStale ? "stale" : null;
  }
  // Anything else, including a missing or unknown status, still needs a human.
  return "proposed";
}

function groupEntries(entries, today) {
  const groups = Object.fromEntries(GROUPS.map((group) => [group.key, []]));
  for (const entry of entries) {
    const key = classify(entry, today);
    if (key) {
      groups[key].push(entry);
    }
  }
  return groups;
}

function buildIssueBody(groups, fileUrl) {
  const sections = GROUPS.filter((group) => groups[group.key].length > 0).map((group) => {
    const items = groups[group.key].map(
      (entry) => `- [ ] [${entry.assumption}](${fileUrl}#L${entry.line})`
    );
    return `### ${group.heading}\n\n${items.join("\n")}`;
  });

  return `${MARKER}
These entries in [\`${REGISTRY_PATH}\`](${fileUrl}) need a human. For each one, set **Status** to \`confirmed\`, \`disputed\`, or \`rejected\`, update **Last verified**, and correct the text if needed. This issue updates monthly and closes itself once nothing is left.

${sections.join("\n\n")}`;
}

async function findOpenIssue(github, context) {
  const issues = await github.paginate(github.rest.issues.listForRepo, {
    owner: context.repo.owner,
    repo: context.repo.repo,
    state: "open",
    per_page: 100,
  });
  return issues.find(
    (issue) => !issue.pull_request && issue.user?.type === "Bot" && issue.body?.includes(MARKER)
  );
}

async function assumptionsReview({ github, context, core }) {
  const markdown = fs.readFileSync(path.join(process.cwd(), REGISTRY_PATH), "utf8");
  const groups = groupEntries(parseEntries(markdown), new Date());
  const needsReview = Object.values(groups).flat().length;

  const { owner, repo } = context.repo;
  const fileUrl = `${context.serverUrl}/${owner}/${repo}/blob/${context.ref.replace("refs/heads/", "")}/${REGISTRY_PATH}?plain=1`;
  const existing = await findOpenIssue(github, context);

  if (needsReview === 0) {
    if (existing) {
      await github.rest.issues.update({ owner, repo, issue_number: existing.number, state: "closed" });
    }
    core.info("No assumptions need review.");
    return;
  }

  const body = buildIssueBody(groups, fileUrl);
  if (existing) {
    await github.rest.issues.update({ owner, repo, issue_number: existing.number, body });
  } else {
    await github.rest.issues.create({ owner, repo, title: ISSUE_TITLE, body });
  }
  core.info(`${needsReview} assumption(s) need review.`);
}

module.exports = assumptionsReview;
module.exports.parseEntries = parseEntries;
module.exports.groupEntries = groupEntries;
module.exports.buildIssueBody = buildIssueBody;
