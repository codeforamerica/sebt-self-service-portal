// Run with: node --test .github/workflows/scripts/assumptions-review.test.js
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");
const { parseEntries, groupEntries, buildIssueBody } = require("./assumptions-review.js");

const TODAY = new Date("2026-10-27T00:00:00Z");

function entry(fields) {
  const lines = Object.entries(fields).map(([name, value]) => `  - **${name}:** ${value}`);
  return ["- **Assumption:** Something is true.", ...lines].join("\n");
}

function groupKeyFor(fields) {
  const groups = groupEntries(parseEntries(entry(fields)), TODAY);
  return Object.keys(groups).find((key) => groups[key].length > 0) ?? null;
}

test("parses assumption text, fields, and line number", () => {
  const markdown = `# Assumptions\n\n## Area\n\n${entry({ Status: "proposed", "Last verified": "never" })}`;
  const [parsed] = parseEntries(markdown);

  assert.equal(parsed.assumption, "Something is true.");
  assert.equal(parsed.line, 5);
  assert.deepEqual(parsed.fields, { status: "proposed", "last verified": "never" });
});

test("proposed entries await review", () => {
  assert.equal(groupKeyFor({ Status: "proposed" }), "proposed");
});

test("entries with a missing status still need review", () => {
  assert.equal(groupKeyFor({ Scope: "All states" }), "proposed");
});

test("disputed entries are flagged", () => {
  assert.equal(groupKeyFor({ Status: "disputed" }), "disputed");
});

test("rejected entries are never flagged, even past their revisit date", () => {
  assert.equal(groupKeyFor({ Status: "rejected", "Revisit by": "2020-01-01" }), null);
});

test("recently verified confirmed entries are not flagged", () => {
  assert.equal(groupKeyFor({ Status: "confirmed", "Last verified": "2026-09-01" }), null);
});

test("confirmed entries verified over 180 days ago are stale", () => {
  assert.equal(groupKeyFor({ Status: "confirmed", "Last verified": "2026-01-01" }), "stale");
});

test("confirmed entries never verified, or with a malformed date, are stale", () => {
  assert.equal(groupKeyFor({ Status: "confirmed", "Last verified": "never" }), "stale");
  assert.equal(groupKeyFor({ Status: "confirmed", "Last verified": "last spring" }), "stale");
});

test("a passed revisit date takes priority over status", () => {
  assert.equal(
    groupKeyFor({ Status: "confirmed", "Last verified": "2026-10-01", "Revisit by": "2026-10-27" }),
    "pastRevisit"
  );
});

test("a future revisit date does not flag a fresh confirmed entry", () => {
  assert.equal(
    groupKeyFor({ Status: "confirmed", "Last verified": "2026-10-01", "Revisit by": "2027-01-01" }),
    null
  );
});

test("issue body links each entry to its line and omits empty groups", () => {
  const groups = groupEntries(parseEntries(entry({ Status: "disputed" })), TODAY);
  const body = buildIssueBody(groups, "https://example.test/assumptions.md?plain=1");

  assert.match(body, /<!-- assumptions-review -->/);
  assert.match(body, /- \[ \] \[Something is true\.\]\(https:\/\/example\.test\/assumptions\.md\?plain=1#L1\)/);
  assert.match(body, /Disputed/);
  assert.doesNotMatch(body, /Proposed, awaiting review/);
});

test("every entry in the real registry parses with a status", () => {
  const markdown = fs.readFileSync(path.join(__dirname, "../../../docs/assumptions.md"), "utf8");
  const entries = parseEntries(markdown);

  assert.ok(entries.length > 0);
  for (const parsed of entries) {
    assert.ok(parsed.fields.status, `Entry on line ${parsed.line} has no Status`);
  }
});
