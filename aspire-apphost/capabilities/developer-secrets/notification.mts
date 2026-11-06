// Shows where secrets come from in the dashboard, because `aspire run` keeps AppHost output in its log file.

import { MessageIntent } from "../../.aspire/modules/aspire.mjs";
import type { DistributedApplicationBuilder } from "../../.aspire/modules/aspire.mjs";

export async function notifyDeveloperSecrets(
  builder: DistributedApplicationBuilder,
  message: string,
): Promise<void> {
  await builder.subscribeAfterResourcesCreated(async (event) => {
    const interaction = await event.services().getInteractionService();
    if (!(await interaction.isAvailable())) {
      return;
    }
    // The prompt resolves only when someone dismisses it, so awaiting it would hold up the start.
    void interaction
      .promptNotification("Secrets", message, { intent: MessageIntent.Information })
      .catch((error: unknown) => {
        console.warn(`[secrets] Couldn't show the dashboard notification: ${String(error)}`);
      });
  });
}
