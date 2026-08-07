// Fails the check if a PR has none of the state labels (co, dc, all states), and
// posts (or updates) a sticky comment explaining what's missing. Reacts to
// opened/labeled/unlabeled/synchronize/reopened, so the check clears itself the
// moment a qualifying label is added, without needing a new commit/push.
//
// Uses a hidden marker (not visible text, unlike post-plan-comment.js's approach)
// to find the same comment across runs, since this comment's visible text genuinely
// differs between the "missing" and "resolved" states -- a text-based search would
// stop matching once resolved, and the next failure would create a duplicate
// comment instead of reusing it.
const STATE_LABELS = ["co", "dc", "all states"];
const MARKER = "<!-- pr-require-state-label -->";

const MISSING_BODY = `${MARKER}
⚠️ This PR needs a label so we know which states it affects. Add one of:

- \`co\` — Colorado only
- \`dc\` — DC only
- \`all states\` — applies everywhere (use this for infra/platform changes that aren't state-specific)

While you're at it, a type label (\`feature\`, \`bugfix\`, \`chore\`, \`refactor\`, …) helps too — most are auto-applied from your branch name, see \`.github/labeler.yml\` if yours wasn't.`;

const RESOLVED_BODY = `${MARKER}
✅ State label added — thanks!`;

module.exports = async ({ github, context, core }) => {
  const pr = context.payload.pull_request;
  const labels = pr.labels.map((l) => l.name.toLowerCase());
  const hasStateLabel = STATE_LABELS.some((label) => labels.includes(label));

  const { data: comments } = await github.rest.issues.listComments({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: pr.number,
  });
  const existing = comments.find(
    (comment) => comment.user.type === "Bot" && comment.body.includes(MARKER)
  );

  if (!hasStateLabel) {
    if (existing) {
      await github.rest.issues.updateComment({
        owner: context.repo.owner,
        repo: context.repo.repo,
        comment_id: existing.id,
        body: MISSING_BODY,
      });
    } else {
      await github.rest.issues.createComment({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: pr.number,
        body: MISSING_BODY,
      });
    }
    core.setFailed("Missing a state label (co, dc, or all states)");
    return;
  }

  if (existing) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: existing.id,
      body: RESOLVED_BODY,
    });
  }
};
