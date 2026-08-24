// Aspire AppHost for the SEBT Self-Service Portal.
//
// The resource graph is composed per state: the shared resources plus exactly one
// state's resources. DC and CO need genuinely different dependencies — DC a DcSource
// database and Mailpit, CO Redis and a Keycloak IdP — so the graph is built per state
// rather than modeling every state and starting a subset.
//
// Adding a state: add a states/<state>.mts module and one case below.
//
// Usage: STATE=dc aspire run | STATE=co aspire run

import { createBuilder } from "./.aspire/modules/aspire.mjs";
import { addApi, addWebApps, type SupportedState } from "./states/apps.mjs";
import { addCoResources } from "./states/co.mjs";
import { addDcResources } from "./states/dc.mjs";
import { addSharedResources } from "./states/shared.mjs";

const supportedStates: readonly SupportedState[] = ["dc", "co"];

function resolveState(): SupportedState {
  // Defaults to dc, matching compose.yaml's `${STATE:-dc}` and `pnpm dev`.
  const requested = (process.env.STATE ?? "dc").toLowerCase();

  if (!supportedStates.includes(requested as SupportedState)) {
    throw new Error(
      `Unsupported STATE '${requested}'. Supported states: ${supportedStates.join(", ")}.`,
    );
  }

  return requested as SupportedState;
}

const state = resolveState();
console.log(`[apphost] composing resource graph for STATE=${state}`);

const builder = await createBuilder();

const shared = await addSharedResources(builder);
const api = await addApi(builder, state, shared);

// State modules attach their own API environment and waits to the resource above.
switch (state) {
  case "dc":
    await addDcResources(builder, shared, api);
    break;
  case "co":
    await addCoResources(builder, api);
    break;
}

await addWebApps(builder, state, api);

await builder.build().run();
