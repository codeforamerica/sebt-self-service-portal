// Aspire AppHost for the SEBT Self-Service Portal.
//
// The resource graph is composed per state: the shared resources plus exactly one
// state's resources. DC and CO need genuinely different dependencies — DC a DcSource
// database and Mailpit, CO a Keycloak IdP — so the graph is built per state rather than
// modeling every state and starting a subset.
//
// Adding a state: add a states/<state>.mts module and one case below.
//
// Usage: STATE=dc aspire run | STATE=co aspire run

import { createBuilder } from './.aspire/modules/aspire.mjs';
import { addSharedResources } from './states/shared.mjs';

const supportedStates = ['dc', 'co'] as const;
type SupportedState = (typeof supportedStates)[number];

function resolveState(): SupportedState {
  // Defaults to dc, matching compose.yaml's `${STATE:-dc}` and `pnpm dev`.
  const requested = (process.env.STATE ?? 'dc').toLowerCase();

  if (!supportedStates.includes(requested as SupportedState)) {
    throw new Error(
      `Unsupported STATE '${requested}'. Supported states: ${supportedStates.join(', ')}.`,
    );
  }

  return requested as SupportedState;
}

const state = resolveState();
console.log(`[apphost] composing resource graph for STATE=${state}`);

const builder = await createBuilder();

await addSharedResources(builder);

// TODO(DC-713): add state-specific resources.
//   dc -> states/dc.mts (DcSource SQL Server + seed, Mailpit, DC plugin build)
//   co -> states/co.mts (Keycloak)

await builder.build().run();
