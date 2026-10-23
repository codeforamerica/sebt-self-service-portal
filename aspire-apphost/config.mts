// The configuration surface of the AppHost.
//
// This module is the one that reads process.env. Thus each value that a developer can
// change is declared here, with its default. A resource module gets the resolved
// configuration, and it does not read the environment.
//
// The launch command selects the state, and the run time does not. The commands are
// `pnpm aspire:dc` and `pnpm aspire:co`. STATE decides which resources exist, and Aspire
// composes the resource graph one time at start. Thus a new state needs a new start of
// the AppHost.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

export type SupportedState = "dc" | "co";

export const supportedStates: readonly SupportedState[] = ["dc", "co"];

/** The repository root, from the aspire-apphost directory. */
export const repoRoot = resolve(import.meta.dirname, "..");

export interface AppHostConfig {
  /** The state whose resource graph this AppHost composes. */
  state: SupportedState;
  /**
   * The SA password for the 2 SQL Server instances. It agrees with compose.yaml and
   * appsettings.json. A deployed environment does not use it.
   */
  sqlPassword: string;
  /** Aspire needs authentication for Redis. Compose runs Redis with no authentication. */
  redisPassword: string;
  /**
   * The checkout of the DC connector, which is outside this repository. It holds
   * Dockerfile.seed and scripts/sql.
   */
  dcConnectorPath: string;
  /**
   * The address where the dashboard accepts OTLP. Aspire sets it from the launch profile
   * in aspire.config.json. Thus the value is absent when the AppHost runs with no launch
   * profile. The value is optional and it has no default, because an incorrect address is
   * worse than no address.
   */
  dashboardOtlpEndpoint: string | undefined;
}

function resolveState(): SupportedState {
  // The default is dc. This agrees with `${STATE:-dc}` in compose.yaml and with
  // `pnpm dev`.
  const requested = (process.env.STATE ?? "dc").toLowerCase();

  if (!supportedStates.includes(requested as SupportedState)) {
    throw new Error(
      `Unsupported STATE '${requested}'. Supported states: ${supportedStates.join(", ")}.`,
    );
  }

  return requested as SupportedState;
}

function resolveDcConnectorPath(state: SupportedState): string {
  const path =
    process.env.DC_CONNECTOR_PATH ??
    resolve(repoRoot, "..", "sebt-self-service-portal-dc-connector");

  // DC is the state that needs the checkout. Thus CO must not fail when the checkout is
  // absent.
  if (state === "dc" && !existsSync(path)) {
    throw new Error(
      `DC connector checkout not found at '${path}'. Clone it beside this repo, or set DC_CONNECTOR_PATH.`,
    );
  }

  return path;
}

export function loadConfig(): AppHostConfig {
  const state = resolveState();

  return {
    state,
    sqlPassword: process.env.MSSQL_SA_PASSWORD ?? "YourStrong@Passw0rd",
    redisPassword: process.env.REDIS_PASSWORD ?? "LocalDevRedis1!",
    dcConnectorPath: resolveDcConnectorPath(state),
    dashboardOtlpEndpoint: process.env.ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL,
  };
}
