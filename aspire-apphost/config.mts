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

/** The Doppler project that holds local development secrets. A team with its own project changes this. */
export const dopplerProject = "safety-net-sebt-self-service-portal";

/** The only Doppler environment the AppHost loads, so deployed keys never reach a laptop. */
export const dopplerEnvironment = "local";

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
  /** Whether to load secrets from Doppler when this repo is set up for it. USE_DOPPLER=false turns it off. */
  useDoppler: boolean;
}

/** Reads USE_DOPPLER: unset or empty uses Doppler when it is set up, and `false` or `0` turns it off. */
export function parseUseDoppler(raw: string | undefined): boolean {
  const value = (raw ?? "").trim().toLowerCase();

  if (value === "") {
    return true;
  }
  if (value === "false" || value === "0") {
    return false;
  }

  throw new Error(
    `USE_DOPPLER must be false or 0 to turn Doppler off, or unset to use Doppler when it is set up, but was '${raw}'.`,
  );
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
    useDoppler: parseUseDoppler(process.env.USE_DOPPLER),
  };
}
