// CO has no local stand-in for CBMS. Thus this capability makes no resource. It turns
// on the 2 mock switches and it tells the seed which state to make data for.
//
// This is the smallest shape that a provider can have. It shows that a capability is
// about the requirement, and not about the infrastructure: CO answers the same question
// as DC, and it answers it with configuration alone.

import type {
  HouseholdSourceCapability,
  HouseholdSourceProvider,
} from "./household-source.mjs";

// The context is not in the parameter list, because this provider needs nothing from it.
// The type of HouseholdSourceProvider still accepts this function.
function provision(): Promise<HouseholdSourceCapability> {
  return Promise.resolve({
    requirements: {
      settings: [
        {
          key: "UseMockHouseholdData",
          value: "true",
          why: "Sends the household reads and writes of the portal to MockHouseholdRepository.",
        },
        // There are 2 mock switches, and both are necessary. UseMockHouseholdData above
        // belongs to the portal. The switch below belongs to the CO connector. That
        // connector makes its own CBMS HTTP client, and it needs Cbms:ClientId and
        // Cbms:ClientSecret. Without the switch, the co-cbms-api-ping health check of
        // the connector reports Degraded on a checkout that has no credentials.
        {
          key: "Cbms__UseMockResponses",
          value: "true",
          why: "Stops the CO connector from asking for CBMS credentials that a local checkout does not have.",
        },
        {
          key: "Seeding__State",
          value: "co",
          why: "Tells the seed which state to make households for.",
        },
      ],
      // Nothing to wait for. The mock data is in the process of the API.
      waits: [],
    },
  });
}

export const coMockCbms: HouseholdSourceProvider = {
  name: "mock CBMS, in the process of the API",
  dataHint:
    "Households come from MockHouseholdRepository.SeedMockData; the sign-in capability sets the email pattern that connects them to the users of the realm.",
  // No resource and no file. Thus there is no obligation on the host machine. This list
  // is empty by design, and not by omission.
  preflight: () => [],
  provision,
};
