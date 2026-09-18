// Aspire AppHost for the SEBT Self-Service Portal.
//
// The resource graph is composed per state: the shared resources plus exactly one
// state's resources. DC and CO need genuinely different dependencies — DC a DcSource
// database and Mailpit, CO Redis and a Keycloak IdP — so the graph is built per state
// rather than modeling every state and starting a subset.
//
// Every knob, including which state to compose, comes from ./config.mts.
// Adding a state: add a states/<state>.mts module and one case below.
//
// Usage: pnpm aspire:dc | pnpm aspire:co

import { createBuilder } from "./.aspire/modules/aspire.mjs";
import { loadConfig } from "./config.mjs";
import { addApi, addWebApps } from "./states/apps.mjs";
import { addCoResources, wireCoPortalCallback } from "./states/co.mjs";
import type { CoResources } from "./states/co.mjs";
import { addDcResources } from "./states/dc.mjs";
import { addSharedResources } from "./states/shared.mjs";

const config = loadConfig();
console.log(`[apphost] composing resource graph for STATE=${config.state}`);

const builder = await createBuilder();

const shared = await addSharedResources(builder, config);
const api = await addApi(builder, config, shared);

// State modules attach their own API environment and waits to the resource above.
let co: CoResources | undefined;

switch (config.state) {
  case "dc":
    await addDcResources(builder, config, shared, api);
    break;
  case "co":
    co = await addCoResources(builder, config, api);
    break;
}

const apps = await addWebApps(builder, config, api);

// Last because it is the one piece of state wiring that reads the portal's endpoint:
// CO's realm redirects back to it, so neither side can hardcode a port.
if (co) {
  await wireCoPortalCallback(api, co, apps.web);
}

await builder.build().run();
