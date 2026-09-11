// The AppHost's configuration surface.
//
// This is the only module that reads process.env, so every knob a developer might need
// to change is declared and defaulted in one place. Resource modules take the resolved
// config and never consult the environment themselves.
//
// State is chosen by the launch command (`pnpm aspire:dc` / `pnpm aspire:co`) rather
// than at runtime: STATE decides which resources exist, and Aspire composes the resource
// graph once at startup, so changing it means relaunching the AppHost.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

export type SupportedState = "dc" | "co";

export const supportedStates: readonly SupportedState[] = ["dc", "co"];

/** aspire-apphost/ -> repo root. */
export const repoRoot = resolve(import.meta.dirname, "..");

export interface AppHostConfig {
  /** Which state's resource graph to compose. */
  state: SupportedState;
  /**
   * SA password for both SQL Server instances. Matches compose.yaml and
   * appsettings.json; never used in a deployed environment.
   */
  sqlPassword: string;
  /** Aspire requires Redis auth; compose runs Redis unauthenticated. */
  redisPassword: string;
  /** Checkout of the out-of-tree DC connector, holding Dockerfile.seed and scripts/sql. */
  dcConnectorPath: string;
}

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

function resolveDcConnectorPath(state: SupportedState): string {
  const path =
    process.env.DC_CONNECTOR_PATH ??
    resolve(repoRoot, "..", "sebt-self-service-portal-dc-connector");

  // Only DC needs the checkout, so CO must not fail on its absence.
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
  };
}
