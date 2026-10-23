// Aspire AppHost for the SEBT Self-Service Portal.
//
// This AppHost composes the resource graph for one state. DC and CO need different
// dependencies. DC needs a DcSource database. CO needs Redis and an OIDC provider. Thus
// the AppHost builds one graph for one state. It does not model each state and start a
// subset.
//
// Two kinds of module give the resources:
//
//   shared/       the resources that each state uses and no state changes. These are the
//                 API, the 2 web applications, and the database of the portal.
//   capabilities/ the infrastructure that a state needs to satisfy one requirement of
//                 the application. A capability is a provider. The provider returns what
//                 it needs, and it does not change the API.
//
// One directory holds each capability:
//
//   capabilities/requirements.mts           the shape that each capability speaks in.
//   capabilities/<capability>/contract.mts  the provider type and the map of the states.
//   capabilities/<capability>/dc.mts        the answer of DC.
//   capabilities/<capability>/co.mts        the answer of CO.
//
// The 5 capabilities are the cache, the connector build, the household source, sign-in,
// and telemetry. To compare 2 states, read the 2 files in one directory.
//
// Telemetry is the one capability where the 2 states agree, so it holds one provider in
// dashboard.mts instead of a file for each state.
//
// The wiring is in ./compose.mts, and not here. This file is the entry point only.
//
// Each value, and also the state, comes from ./config.mts.
// To add a state, add one provider for each capability. The type of each registry is
// `Record<SupportedState, ...>`, so the compiler gives the list of what is absent.
//
// Usage: pnpm aspire:dc or pnpm aspire:co

import { createBuilder } from "./.aspire/modules/aspire.mjs";
import {
  announceCapabilities,
  composeGraph,
  runCapabilityPreflight,
} from "./compose.mjs";
import { loadConfig } from "./config.mjs";

const config = loadConfig();
console.log(`[apphost] composing resource graph for STATE=${config.state}`);

announceCapabilities(config);
await runCapabilityPreflight(config);

const builder = await createBuilder();
await composeGraph(builder, config);
await builder.build().run();
