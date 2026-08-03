// Posts (or updates) a single PR comment summarizing the dev-dc and dev-co tofu plans.
// Used by deploy-ecr.yaml's plan-comment job via actions/github-script's require()
// support. Expects DC_PLAN/CO_PLAN env vars rather than direct `${{ }}` interpolation
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

  const planLine = summary.match(/Plan: \d+ to add, \d+ to change, \d+ to destroy\./);
  if (planLine) {
    summary = summary.slice(0, planLine.index + planLine[0].length);
  }
  return summary;
}

// Splits `budget` characters between two plans. A plan shorter than its even share just
// uses what it needs; the unused portion goes to the other plan instead of being wasted
// on a fixed 50/50 split.
function splitBudget(a, b, budget) {
  const half = Math.floor(budget / 2);
  if (a.length <= half) return [a.length, Math.min(b.length, budget - a.length)];
  if (b.length <= half) return [Math.min(a.length, budget - b.length), b.length];
  return [half, budget - half];
}

function truncate(plan, limit) {
  if (plan.length <= limit) return plan;

  // Preserve the trailing "Plan: X to add..." summary line -- the single most useful
  // line for a quick scan -- by cutting from the middle instead of the tail. Falls back
  // to a plain head-truncation when there's no summary line to preserve (e.g. the
  // no-changes case, which is short enough to never hit this path anyway, or an error).
  const tailMatch = plan.match(/\n?Plan: \d+ to add, \d+ to change, \d+ to destroy\.\s*$/);
  const tail = tailMatch ? tailMatch[0] : "";
  const headBudget = Math.max(0, limit - tail.length - TRUNCATION_NOTICE.length);
  return plan.slice(0, headBudget) + TRUNCATION_NOTICE + tail;
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

  let dcPlan = extractPlanSummary(process.env.DC_PLAN) || "";
  let coPlan = extractPlanSummary(process.env.CO_PLAN) || "";

  const budget = COMMENT_LIMIT - 2 * MARKDOWN_OVERHEAD_PER_SECTION;
  if (dcPlan.length + coPlan.length > budget) {
    const [dcLimit, coLimit] = splitBudget(dcPlan, coPlan, budget);
    dcPlan = truncate(dcPlan, dcLimit);
    coPlan = truncate(coPlan, coLimit);
  }

  let output = "";
  if (dcPlan) {
    output += `## Plan output — dev-dc\n\n\`\`\`\n${dcPlan}\n\`\`\`\n\n`;
  }
  if (coPlan) {
    output += `## Plan output — dev-co\n\n\`\`\`\n${coPlan}\n\`\`\``;
  }

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
