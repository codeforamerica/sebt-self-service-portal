// CO has no local stand-in for CBMS, so this capability makes no resource. A developer's
// appsettings.co.json chooses between mock data and the CBMS UAT environment. Without
// that file, this capability turns the mocks on so a fresh checkout still has households.
//
// This is the smallest shape that a provider can have. It shows that a capability is
// about the requirement, and not about the infrastructure: CO answers the same question
// as DC, and it answers it with configuration alone.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { repoRoot } from "../../config.mjs";
import { notifyDashboard } from "../dashboard-notification.mjs";
import type { ConfigSetting } from "../requirements.mjs";
import type {
  HouseholdSourceCapability,
  HouseholdSourceContext,
  HouseholdSourceProvider,
} from "./contract.mjs";

/** The developer's own CO settings. It is gitignored, so only some checkouts have it. */
const localSettingsFile = resolve(
  repoRoot,
  "apps/portal/src/SEBT.Portal.Api/appsettings.co.json",
);

/**
 * The settings the API needs for CO household data. Aspire's env vars outrank
 * appsettings.co.json, so when that file exists the mock choices are left to it.
 */
export function coHouseholdSettings(hasLocalSettings: boolean): ConfigSetting[] {
  const settings: ConfigSetting[] = [
    {
      key: "Seeding__State",
      value: "co",
      why: "Tells the seed which state to make households for.",
    },
    // appsettings.co.example.json sets Phone, and the base appsettings.json sets
    // Email. Email cannot resolve after an OIDC login, because that login stores a
    // user with no email and HouseholdIdentifierResolver reads an email from the user
    // record alone. Phone comes from the token, and the fixture users of the realm
    // carry the phones of the co-loaded and co-active mock households.
    {
      key: "StateHouseholdId__PreferredHouseholdIdTypes__0",
      value: "Phone",
      why: "The key that the portal looks a household up by. Only Phone resolves for a user who signed in through OIDC.",
    },
  ];

  if (hasLocalSettings) {
    return settings;
  }

  settings.push(
    {
      key: "FeatureManagement__use_mock_household_data",
      value: "true",
      why: "Sends the household reads and writes of the portal to MockHouseholdRepository.",
    },
    // use_mock_household_data belongs to the portal, and use_mock_cbms_responses to the
    // CO connector. Without this one, the connector's co-cbms-api-ping health check
    // reports Degraded on a checkout that has no CBMS credentials.
    {
      key: "FeatureManagement__use_mock_cbms_responses",
      value: "true",
      why: "Stops the CO connector from asking for CBMS credentials that a local checkout does not have.",
    },
    {
      key: "DevelopmentPhoneOverride__Phone",
      value: "",
      why: "The mock households match the phones of the realm's fixture users, and an override would load a household the signed-in user does not own.",
      kind: "neutralize",
    },
  );
  return settings;
}

/** Tells the developer where the choice of household data lives. */
export function coHouseholdMessage(hasLocalSettings: boolean): string {
  if (hasLocalSettings) {
    return (
      "Household data comes from appsettings.co.json. Set use_mock_household_data and " +
      "use_mock_cbms_responses to false for the CBMS UAT environment, or set " +
      "use_mock_household_data to true for mock data. DevelopmentPhoneOverride:Phone " +
      "picks the account to load."
    );
  }

  return (
    "Using mock household data. To use the CBMS UAT environment, copy " +
    "appsettings.co.example.json to appsettings.co.json and set use_mock_household_data " +
    "and use_mock_cbms_responses to false. DevelopmentPhoneOverride:Phone picks the " +
    "account to load."
  );
}

async function provision({
  builder,
}: HouseholdSourceContext): Promise<HouseholdSourceCapability> {
  const hasLocalSettings = existsSync(localSettingsFile);
  const message = coHouseholdMessage(hasLocalSettings);
  console.log(`[household-source] ${message}`);
  await notifyDashboard(builder, "Household data", message);

  return {
    requirements: {
      settings: coHouseholdSettings(hasLocalSettings),
      // Nothing to wait for. The mock data is in the process of the API, and CBMS UAT
      // is not a resource of this graph.
      waits: [],
    },
  };
}

export const coHouseholdSource: HouseholdSourceProvider = {
  name: "appsettings.co.json, or mock data in the process of the API",
  dataHint:
    "appsettings.co.json chooses mock data or the CBMS UAT environment when it exists; without it, households come from MockHouseholdRepository.SeedMockData.",
  // No resource, and a missing settings file is a supported case. Thus there is no
  // obligation on the host machine. This list is empty by design, and not by omission.
  preflight: () => [],
  provision,
};
