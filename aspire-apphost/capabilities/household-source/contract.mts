// The household source capability: where the portal reads the data of a household, and
// what that choice needs from the other parts of the graph.
//
// DC and CO differ here as much as they differ for sign-in. DC reads a SQL database that
// is a stand-in for the ESA_LINK system of the state, and a job must put data in that
// database first. CO has no local stand-in for CBMS, so the portal answers from its own
// mock repository and the connector answers with mock responses. One provider makes 3
// resources, and the other makes none.
//
// The contract has the same shape as the sign-in contract in ./sign-in.mts. A provider
// makes the resources that it owns, and it returns what those resources need. It does
// not change the API. Read ./requirements.mts.

import type {
  DistributedApplicationBuilder,
  ParameterResource,
} from "../../.aspire/modules/aspire.mjs";
import type { AppHostConfig, SupportedState } from "../../config.mjs";
import type { Preflight, Requirements } from "../requirements.mjs";
import { coMockCbms } from "./co.mjs";
import { dcSourceDatabase } from "./dc.mjs";

export interface HouseholdSourceContext {
  builder: DistributedApplicationBuilder;
  config: AppHostConfig;
  /**
   * The SA password of the shared SQL Server. A provider that makes its own SQL Server
   * uses the same password. This is the one part of the shared graph that a provider
   * needs, so the context names it and it does not take all of PortalDatabase.
   */
  saPassword: ParameterResource;
}

export interface HouseholdSourceCapability {
  requirements: Requirements;
}

export interface HouseholdSourceProvider {
  /** Where this state reads household data. This reads as a row of the capability matrix. */
  readonly name: string;
  /** Where the data of a test household comes from. A new person asks this second. */
  readonly dataHint: string;
  /**
   * The obligations of the host machine. The checks run before any resource exists.
   *
   * This is a function of the configuration, and not a fixed list. The paths of DC come
   * from DC_CONNECTOR_PATH, which only the resolved configuration knows.
   */
  readonly preflight: (config: AppHostConfig) => readonly Preflight[];
  provision(context: HouseholdSourceContext): Promise<HouseholdSourceCapability>;
}

// The map has a key for each state, and the AppHost does not use a switch. Thus a new
// state in SupportedState is an error of compilation here, until that state has a
// household source.
const providers: Record<SupportedState, HouseholdSourceProvider> = {
  dc: dcSourceDatabase,
  co: coMockCbms,
};

export function householdSourceProviderFor(
  state: SupportedState,
): HouseholdSourceProvider {
  return providers[state];
}
