// The cache capability: where the portal keeps the household data that it reads again,
// and what that choice needs from the other parts of the graph.
//
// This is the capability with the largest difference between the 2 states, and the
// difference is that one state has nothing. CO declares a `Redis` section in its
// configuration, and Redis holds the CBMS household cache. DC declares no such section,
// and `HybridCache` keeps level 1 in the process of the API. Thus the provider of DC
// makes no resource and gives no setting.
//
// An empty provider is a statement, and not an omission. It says that DC answered this
// question, and that the answer is "nothing". Read ./requirements.mts.

import type { DistributedApplicationBuilder } from "../.aspire/modules/aspire.mjs";
import type { AppHostConfig, SupportedState } from "../config.mjs";
import { coRedis } from "./cache-co.mjs";
import { dcNoCache } from "./cache-dc.mjs";
import type { Preflight, Requirements } from "./requirements.mjs";

export interface CacheContext {
  builder: DistributedApplicationBuilder;
  config: AppHostConfig;
}

export interface CacheCapability {
  requirements: Requirements;
}

export interface CacheProvider {
  /** What holds the cache of this state. This reads as a row of the capability matrix. */
  readonly name: string;
  /** What a developer can open to look at the cache. */
  readonly cacheHint: string;
  /** The obligations of the host machine. The checks run before any resource exists. */
  readonly preflight: (config: AppHostConfig) => readonly Preflight[];
  provision(context: CacheContext): Promise<CacheCapability>;
}

// The map has a key for each state, and the AppHost does not use a switch. Thus a new
// state in SupportedState is an error of compilation here. A new state must answer this
// question, and `dcNoCache` shows how to answer it with "nothing".
const providers: Record<SupportedState, CacheProvider> = {
  dc: dcNoCache,
  co: coRedis,
};

export function cacheProviderFor(state: SupportedState): CacheProvider {
  return providers[state];
}
