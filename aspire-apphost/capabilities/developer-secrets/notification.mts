// Shows where secrets come from in the dashboard, because `aspire run` keeps AppHost output in its log file.

import type { DistributedApplicationBuilder } from "../../.aspire/modules/aspire.mjs";
import { notifyDashboard } from "../dashboard-notification.mjs";

export function notifyDeveloperSecrets(
  builder: DistributedApplicationBuilder,
  message: string,
): Promise<void> {
  return notifyDashboard(builder, "Secrets", message);
}
