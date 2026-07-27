// Posts (or updates) a single PR comment summarizing the dev-dc and dev-co tofu plans.
// Used by deploy-ecr.yaml's plan-comment job via actions/github-script's require()
// support. Expects DC_PLAN/CO_PLAN env vars rather than direct `${{ }}` interpolation
// into script source, since embedding large/arbitrary plan text directly into a JS
// template literal is the same pattern GitHub's own security guidance warns against
// (script injection via untrusted input) -- low risk here since the content is our own
// plan output, but the fix is free and avoids the anti-pattern either way.
//
// Full plan output also includes one "Refreshing state..."/"Reading..." line per
// resource, which dwarfs the actual diff on a repo this size and can make the combined
// dc+co comment too large for actions/github-script's underlying node process to even
// start (an OS argument/environment size limit, not a GitHub API rejection). Keep only
// the actual plan: from the summary/actions header down through the "Plan: X to add..."
// line, dropping the refresh noise before it and the deprecation warnings after it. A
// hard length cap is a safety net in case even that section is still too large, or the
// expected markers aren't found (e.g. a real error, which is shown in full rather than
// silently cut).
const MAX_PLAN_LENGTH = 30000;

function summarizePlan(plan) {
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
  // Couldn't find a known marker -- likely an error. Show it in full (subject to the
  // length cap below) rather than hide it.
  let summary = startIndex === -1 ? plan : plan.slice(startIndex);

  const planLine = summary.match(/Plan: \d+ to add, \d+ to change, \d+ to destroy\./);
  if (planLine) {
    summary = summary.slice(0, planLine.index + planLine[0].length);
  }

  if (summary.length > MAX_PLAN_LENGTH) {
    summary = summary.slice(0, MAX_PLAN_LENGTH) + "\n\n... (truncated — see the full output in the workflow run)";
  }
  return summary;
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

  const dcPlan = summarizePlan(process.env.DC_PLAN);
  const coPlan = summarizePlan(process.env.CO_PLAN);

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
