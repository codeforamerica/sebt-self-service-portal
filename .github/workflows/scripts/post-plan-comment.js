// Posts (or updates) a single PR comment summarizing the tofu plans for every config.
// Used by deploy-ecr.yaml's plan-comment job via actions/github-script's require()
// support. Expects the plans in env vars (see PLANS) rather than direct `${{ }}` interpolation
// into script source, since embedding large/arbitrary plan text directly into a JS
// template literal is the same pattern GitHub's own security guidance warns against
// (script injection via untrusted input) -- low risk here since the content is our own
// plan output, but the fix is free and avoids the anti-pattern either way.
//
// Two independent size constraints to account for:
//   1. GitHub hard-rejects comment bodies over 65536 characters (a 422 error) -- a real,
//      unavoidable limit no matter how the plan itself is generated.
//   2. Full plan output used to include one "Refreshing state..."/"Reading..." line per
//      resource, dwarfing the actual diff on a repo this size. That's now handled at the
//      source by running `tofu plan -concise`. extractPlanSummary below still trims from
//      the actions header through the "Plan: X to add..." line (in case -concise ever
//      leaves something behind, and to drop the trailing deprecation warnings), and
//      falls back to showing the full text if no known markers are found, so a real
//      error isn't silently hidden.
const COMMENT_LIMIT = 65536;
const MARKDOWN_OVERHEAD_PER_SECTION = 60; // "## Plan output — dev-xx\n\n```\n" + "\n```\n\n"
const TRUNCATION_NOTICE = "\n\n... (truncated — see the full output in the workflow run)";
const PLAN_LINE = "Plan: (?:\\d+ to import, )?\\d+ to add, \\d+ to change, \\d+ to destroy(?:, \\d+ to forget)?\\.";

// In apply order: each account's shared config before the configs that use it.
const PLANS = [
  { label: "dev-dc-shared", env: "DC_SHARED_PLAN" },
  { label: "dev-dc", env: "DC_PLAN" },
  { label: "dev-co-shared", env: "CO_SHARED_PLAN" },
  { label: "dev-co", env: "CO_PLAN" },
  { label: "dev-demo", env: "DEMO_PLAN" },
];

function extractPlanSummary(plan) {
  if (!plan) return plan;

  const startMarkers = [
    "OpenTofu used the selected providers to generate the following execution plan",
    "No changes. Your infrastructure matches the configuration.",
  ];
  let startIndex = -1;
  for (const marker of startMarkers) {
    const idx = plan.indexOf(marker);
    if (idx !== -1 && (startIndex === -1 || idx < startIndex)) {
      startIndex = idx;
    }
  }
  // Couldn't find a known marker -- likely an error. Show it in full rather than hide it.
  let summary = startIndex === -1 ? plan : plan.slice(startIndex);

  const planLine = summary.match(new RegExp(PLAN_LINE));
  if (planLine) {
    summary = summary.slice(0, planLine.index + planLine[0].length);
  }
  return summary;
}

// Splits `budget` characters across plans. A plan shorter than its even share just
// uses what it needs; the unused portion goes to the longer plans instead of being
// wasted on a fixed even split.
function splitBudget(lengths, budget) {
  const limits = new Array(lengths.length);
  const shortestFirst = lengths.map((length, index) => ({ length, index })).sort((a, b) => a.length - b.length);
  let remaining = budget;
  shortestFirst.forEach(({ length, index }, position) => {
    const share = Math.floor(remaining / (shortestFirst.length - position));
    limits[index] = Math.min(length, share);
    remaining -= limits[index];
  });
  return limits;
}

function truncate(plan, limit) {
  if (plan.length <= limit) return plan;

  // Preserve the trailing "Plan: X to add..." summary line -- the single most useful
  // line for a quick scan -- by cutting from the middle instead of the tail. Falls back
  // to a plain head-truncation when there's no summary line to preserve (e.g. the
  // no-changes case, which is short enough to never hit this path anyway, or an error).
  const tailMatch = plan.match(new RegExp(`\\n?${PLAN_LINE}\\s*$`));
  const tail = tailMatch ? tailMatch[0] : "";
  const headBudget = Math.max(0, limit - tail.length - TRUNCATION_NOTICE.length);
  return plan.slice(0, headBudget) + TRUNCATION_NOTICE + tail;
}

function buildCommentBody(plans) {
  const sections = plans
    .map(({ label, plan }) => ({ label, plan: extractPlanSummary(plan) || "" }))
    .filter(({ plan }) => plan);

  const budget = COMMENT_LIMIT - sections.length * MARKDOWN_OVERHEAD_PER_SECTION;
  const limits = splitBudget(sections.map(({ plan }) => plan.length), budget);

  return sections
    .map(({ label, plan }, index) => `## Plan output — ${label}\n\n\`\`\`\n${truncate(plan, limits[index])}\n\`\`\``)
    .join("\n\n");
}

module.exports = async ({ github, context }) => {
  const { data: comments } = await github.rest.issues.listComments({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: context.issue.number,
  });
  const botComment = comments.find(
    (comment) => comment.user.type === "Bot" && comment.body.includes("## Plan output")
  );

  const output = buildCommentBody(PLANS.map(({ label, env }) => ({ label, plan: process.env[env] })));

  if (botComment) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: botComment.id,
      body: output,
    });
  } else {
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: context.issue.number,
      body: output,
    });
  }
};

module.exports.extractPlanSummary = extractPlanSummary;
module.exports.splitBudget = splitBudget;
module.exports.buildCommentBody = buildCommentBody;
module.exports.COMMENT_LIMIT = COMMENT_LIMIT;
