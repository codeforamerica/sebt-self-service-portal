// Turns routed Doppler secrets into Aspire secret parameters, so the dashboard masks the values.

import type { DistributedApplicationBuilder } from "../../.aspire/modules/aspire.mjs";
import type { ConfigSetting } from "../requirements.mjs";
import type { RoutedSecret, RoutedSecrets } from "./routing.mjs";

export interface DeveloperSecretSettings {
  api: ConfigSetting[];
  web: ConfigSetting[];
}

async function toSettings(
  builder: DistributedApplicationBuilder,
  secrets: readonly RoutedSecret[],
): Promise<ConfigSetting[]> {
  const settings: ConfigSetting[] = [];
  for (const secret of secrets) {
    const parameter = await builder.addParameter(secret.parameterName, {
      value: secret.value,
      secret: true,
    });
    settings.push({ key: secret.appKey, value: parameter, why: secret.why });
  }
  return settings;
}

export async function developerSecretSettings(
  builder: DistributedApplicationBuilder,
  routed: RoutedSecrets,
): Promise<DeveloperSecretSettings> {
  return {
    api: await toSettings(builder, routed.api),
    web: await toSettings(builder, routed.web),
  };
}
