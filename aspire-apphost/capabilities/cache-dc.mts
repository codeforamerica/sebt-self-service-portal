// DC has no cache resource. `HybridCache` keeps level 1 in the process of the API, and
// the DC connector uses no distributed cache. The configuration of DC declares no
// `Redis` section.
//
// This provider makes no resource, gives no setting, and needs no wait. It is here so
// that DC answers the question, and so that a person who reads the capability matrix
// sees the answer. The empty return is the answer.

import type { CacheCapability, CacheProvider } from "./cache.mjs";

// The context is not in the parameter list, because this provider needs nothing from it.
// The type of CacheProvider still accepts this function.
function provision(): Promise<CacheCapability> {
  return Promise.resolve({ requirements: { settings: [], waits: [] } });
}

export const dcNoCache: CacheProvider = {
  name: "none, HybridCache level 1 only",
  cacheHint:
    "There is no cache resource to open. HybridCache holds level 1 in the process of the API.",
  preflight: () => [],
  provision,
};
